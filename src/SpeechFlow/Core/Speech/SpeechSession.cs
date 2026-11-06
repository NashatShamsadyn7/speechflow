using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SpeechFlow.Core.Speech;

/// <summary>
/// One dictation session: microphone audio → phrases → recognizer → final text.
/// Audio is queued from the first millisecond, so nothing is lost while the model loads, and
/// <see cref="Complete"/> still transcribes the phrase you were saying. Events fire on worker threads.
/// </summary>
public sealed class SpeechSession
{
    readonly ISpeechRecognizer _recognizer;
    readonly string? _language;
    readonly Channel<(byte[] Pcm, float Rms)> _audio =
        Channel.CreateUnbounded<(byte[], float)>(new UnboundedChannelOptions { SingleReader = true });
    readonly Channel<byte[]> _utterances =
        Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    readonly UtteranceSegmenter _segmenter = new();
    readonly CancellationTokenSource _cts = new();

    public event Action<string>? Preparing;
    public event Action? Ready;
    public event Action? SpeechStarted;
    public event Action<bool>? Transcribing;
    public event Action<string>? Final;
    public event Action<string>? Failed;
    public event Action? Completed;

    /// <param name="language">"ar", "en", or null for automatic detection.</param>
    public SpeechSession(ISpeechRecognizer recognizer, string? language)
    {
        _recognizer = recognizer;
        _language = language;
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

            var transcriber = Task.Run(() => TranscribeLoopAsync(ct), ct);
            try
            {
                await foreach (var (pcm, rms) in _audio.Reader.ReadAllAsync(ct))
                {
                    bool wasSpeaking = _segmenter.InSpeech;
                    var utterance = _segmenter.Add(pcm, rms);
                    if (!wasSpeaking && _segmenter.InSpeech) SpeechStarted?.Invoke();
                    if (utterance is not null) _utterances.Writer.TryWrite(utterance);
                }
                if (_segmenter.Flush() is { } last) _utterances.Writer.TryWrite(last);
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

    async Task TranscribeLoopAsync(CancellationToken ct)
    {
        await foreach (var utterance in _utterances.Reader.ReadAllAsync(ct))
        {
            Transcribing?.Invoke(true);
            try
            {
                var text = await _recognizer.TranscribeAsync(utterance, _language, ct);
                if (!string.IsNullOrWhiteSpace(text)) Final?.Invoke(text);
            }
            finally
            {
                Transcribing?.Invoke(false);
            }
        }
    }
}
