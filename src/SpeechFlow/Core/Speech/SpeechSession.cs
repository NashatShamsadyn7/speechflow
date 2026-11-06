using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SpeechFlow.Core.Speech;

/// <summary>
/// One dictation session: microphone audio → phrases → recognizer → final text.
/// Audio is queued from the first millisecond, so nothing is lost while the model loads, and
/// <see cref="Complete"/> still transcribes the phrase you were saying.
/// With a preview recognizer, the phrase in progress is re-transcribed about once a second and
/// reported through <see cref="Interim"/> so the words appear while you speak.
/// Events fire on worker threads.
/// </summary>
public sealed class SpeechSession
{
    const int PreviewIntervalMs = 700;
    const int MinPreviewMs = 600;

    readonly ISpeechRecognizer _recognizer;
    readonly ISpeechRecognizer? _previewRecognizer;
    readonly string? _language;
    readonly bool _translate;
    readonly Channel<(byte[] Pcm, float Rms)> _audio =
        Channel.CreateUnbounded<(byte[], float)>(new UnboundedChannelOptions { SingleReader = true });
    readonly Channel<byte[]> _utterances =
        Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    readonly UtteranceSegmenter _segmenter = new();
    readonly CancellationTokenSource _cts = new();
    readonly Stopwatch _sincePreview = Stopwatch.StartNew();

    volatile bool _previewReady;
    int _previewBusy;
    int _finalsPending;
    int _phrase; // bumps on every cut so a late preview of a finished phrase is dropped

    public event Action<string>? Preparing;
    public event Action? Ready;
    public event Action? SpeechStarted;
    public event Action<string>? Interim;
    public event Action<bool>? Transcribing;
    public event Action<string>? Final;
    public event Action<string>? Failed;
    public event Action? Completed;

    /// <param name="language">"ar", "en", or null for automatic detection.</param>
    /// <param name="previewRecognizer">Fast local recognizer for live preview, or null for none.</param>
    /// <param name="translate">Type English whatever language is spoken.</param>
    public SpeechSession(ISpeechRecognizer recognizer, string? language,
        ISpeechRecognizer? previewRecognizer = null, bool translate = false)
    {
        _recognizer = recognizer;
        _language = language;
        _previewRecognizer = previewRecognizer;
        _translate = translate;
    }

    public void Start() => _ = Task.Run(RunAsync);

    public void Write(byte[] pcm16, float rms) => _audio.Writer.TryWrite((pcm16, rms));

    /// <summary>No more audio; finish the phrase in progress.</summary>
    public void Complete() => _audio.Writer.TryComplete();

    /// <summary>Stop immediately and drop anything not yet transcribed.</summary>
    public void Cancel() => _cts.Cancel();

    async Task RunAsync()
    {
        var ct = _cts.Token;
        try
        {
            await _recognizer.PrepareAsync(new Progress<string>(m => Preparing?.Invoke(m)), ct);
            Ready?.Invoke();
            if (_previewRecognizer is not null) _ = Task.Run(() => PreparePreviewAsync(ct), ct);

            var transcriber = Task.Run(() => TranscribeLoopAsync(ct), ct);
            try
            {
                await foreach (var (pcm, rms) in _audio.Reader.ReadAllAsync(ct))
                {
                    bool wasSpeaking = _segmenter.InSpeech;
                    var utterance = _segmenter.Add(pcm, rms);
                    if (!wasSpeaking && _segmenter.InSpeech) SpeechStarted?.Invoke();
                    if (utterance is not null) Enqueue(utterance);
                    else MaybePreview(ct);
                }
                if (_segmenter.Flush() is { } last) Enqueue(last);
            }
            finally
            {
                _utterances.Writer.TryComplete();
            }
            await transcriber;
        }
        catch (OperationCanceledException)
        {
        }
        catch (SpeechException ex)
        {
            Failed?.Invoke(ex.Message);
        }
        catch (Exception ex)
        {
            Log.Error("Speech session crashed", ex);
            Failed?.Invoke("Speech recognition error. See the log for details.");
        }
        finally
        {
            Completed?.Invoke();
        }
    }

    void Enqueue(byte[] utterance)
    {
        Interlocked.Increment(ref _phrase);
        Interlocked.Increment(ref _finalsPending);
        _utterances.Writer.TryWrite(utterance);
    }

    async Task PreparePreviewAsync(CancellationToken ct)
    {
        try
        {
            if (!ReferenceEquals(_previewRecognizer, _recognizer))
                await _previewRecognizer!.PrepareAsync(new Progress<string>(m => Preparing?.Invoke("Live preview: " + m)), ct);
            _previewReady = true;
            Ready?.Invoke();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Preview is a nicety; dictation keeps working without it.
            Log.Error("Live preview unavailable", ex);
        }
    }

    /// <summary>Re-transcribes the phrase in progress, unless final text is waiting (finals come first).</summary>
    void MaybePreview(CancellationToken ct)
    {
        if (!_previewReady || !_segmenter.InSpeech) return;
        if (_segmenter.LengthMs < MinPreviewMs || _sincePreview.ElapsedMilliseconds < PreviewIntervalMs) return;
        if (Volatile.Read(ref _finalsPending) > 0) return;
        if (Interlocked.CompareExchange(ref _previewBusy, 1, 0) != 0) return;

        _sincePreview.Restart();
        var audio = _segmenter.Snapshot();
        int phrase = Volatile.Read(ref _phrase);
        _ = Task.Run(async () =>
        {
            try
            {
                var text = await _previewRecognizer!.TranscribeAsync(audio, _language, _translate, ct);
                if (phrase == Volatile.Read(ref _phrase) && !string.IsNullOrWhiteSpace(text)) Interim?.Invoke(text);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Error("Live preview failed", ex);
            }
            finally
            {
                Volatile.Write(ref _previewBusy, 0);
            }
        }, ct);
    }

    async Task TranscribeLoopAsync(CancellationToken ct)
    {
        await foreach (var utterance in _utterances.Reader.ReadAllAsync(ct))
        {
            Transcribing?.Invoke(true);
            try
            {
                var text = await _recognizer.TranscribeAsync(utterance, _language, _translate, ct);
                if (!string.IsNullOrWhiteSpace(text)) Final?.Invoke(text);
            }
            finally
            {
                Interlocked.Decrement(ref _finalsPending);
                Transcribing?.Invoke(false);
            }
        }
    }
}
