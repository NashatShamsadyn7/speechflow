using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;

namespace SpeechFlow.Core.Speech;

/// <summary>
/// Whisper (whisper.cpp) running on this PC. Free, private and offline once the model is downloaded.
/// One instance lives for the whole app so the model is loaded only once.
/// </summary>
public sealed class LocalWhisperRecognizer : ISpeechRecognizer, IDisposable
{
    // A short punctuated example nudges Whisper to punctuate its own output.
    static readonly Dictionary<string, string> Prompts = new()
    {
        ["ar"] = "مرحباً، كيف حالك؟ أنا بخير، شكراً.",
        ["en"] = "Hello, how are you? I'm fine, thanks.",
    };

    readonly SemaphoreSlim _gate = new(1, 1);
    readonly Dictionary<string, WhisperProcessor> _processors = new();
    WhisperFactory? _factory;
    WhisperModel _model;

    public LocalWhisperRecognizer(WhisperModel model) => _model = model;

    public WhisperModel Model => _model;

    /// <summary>Switching model unloads the old one; the next session loads the new one.</summary>
    public void SetModel(WhisperModel model)
    {
        if (model == _model) return;
        _gate.Wait();
        try
        {
            _model = model;
            Unload();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PrepareAsync(IProgress<string> progress, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_factory is not null) return;
            if (!ModelStore.IsDownloaded(_model))
            {
                int lastPercent = -1;
                var download = new Progress<double>(f =>
                {
                    int percent = (int)(f * 100);
                    if (percent == lastPercent) return;
                    lastPercent = percent;
                    progress.Report($"Downloading the speech model (one time only)… {percent}%");
                });
                await ModelStore.DownloadAsync(_model, download, ct);
            }

            progress.Report("Loading the speech model…");
            try
            {
                _factory = await Task.Run(() => WhisperFactory.FromPath(ModelStore.PathOf(_model)), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Error("Loading the Whisper model failed", ex);
                throw new SpeechException("The speech model couldn't be loaded. Delete it in Settings and download it again.", ex);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> TranscribeAsync(byte[] pcm16, string? language, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_factory is null) throw new InvalidOperationException("PrepareAsync must run first.");
            var processor = GetProcessor(language, AudioContextFor(pcm16.Length));

            var samples = new float[pcm16.Length / 2];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = (short)(pcm16[2 * i] | (pcm16[2 * i + 1] << 8)) / 32768f;

            var text = new StringBuilder();
            await foreach (var segment in processor.ProcessAsync(samples, ct))
                text.Append(segment.Text);

            var result = text.ToString().Trim();
            return Hallucinations.IsHallucination(result) ? "" : result;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Whisper always encodes a 30 s window (1500 frames, 50 per second) even for a 3 s phrase.
    /// Encoding only the phrase's length, rounded up to a bucket so processors can be reused,
    /// makes short phrases several times faster.
    /// </summary>
    static int AudioContextFor(int pcmBytes)
    {
        const int FullContext = 1500, Bucket = 250;
        double seconds = pcmBytes / (double)(AudioCapture.SampleRate * 2);
        int frames = (int)Math.Ceiling(seconds * 50) + 64; // margin so the last word isn't clipped
        return Math.Min(FullContext, (frames + Bucket - 1) / Bucket * Bucket);
    }

    WhisperProcessor GetProcessor(string? language, int audioContext)
    {
        var key = $"{language ?? "auto"}:{audioContext}";
        if (_processors.TryGetValue(key, out var existing)) return existing;

        var builder = _factory!.CreateBuilder()
            .WithLanguage(language ?? "auto")
            .WithAudioContextSize(audioContext)
            .WithThreads(Math.Max(1, Environment.ProcessorCount - 1))
            .WithNoContext(); // each utterance stands alone; carrying context makes Whisper repeat itself
        if (language is not null && Prompts.TryGetValue(language, out var prompt))
            builder = builder.WithPrompt(prompt);

        var processor = builder.Build();
        _processors[key] = processor;
        return processor;
    }

    void Unload()
    {
        foreach (var p in _processors.Values) p.Dispose();
        _processors.Clear();
        _factory?.Dispose();
        _factory = null;
    }

    public void Dispose()
    {
        _gate.Wait();
        Unload();
    }
}
