using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SpeechFlow.Core.Speech;
using Xunit;
using Xunit.Abstractions;

namespace SpeechFlow.Tests;

public class LocalWhisperCheck(ITestOutputHelper output)
{
    [Fact]
    public async Task Session_transcribes_two_phrases_separated_by_silence()
    {
        var dir = Environment.GetEnvironmentVariable("SPEECHFLOW_SAMPLES");
        if (dir is null) return;

        // Phrase, 1.5 s of silence, phrase — fed in 50 ms chunks like the microphone.
        var pcm = new List<byte>();
        pcm.AddRange(File.ReadAllBytes(Path.Combine(dir, "sample.wav"))[44..]);
        pcm.AddRange(new byte[32000 * 3 / 2]);
        pcm.AddRange(File.ReadAllBytes(Path.Combine(dir, "sample.wav.2.wav"))[44..]);
        pcm.AddRange(new byte[32000]);

        using var recognizer = new LocalWhisperRecognizer(ModelStore.Find("small"));
        var session = new SpeechSession(recognizer, "en", previewRecognizer: recognizer);
        var clock = new Stopwatch();
        var finals = new List<string>();
        var previews = new List<string>();
        session.Interim += t => { previews.Add(t); output.WriteLine($"  [{clock.ElapsedMilliseconds} ms] preview: {t}"); };
        session.SpeechStarted += () => output.WriteLine($"  [{clock.ElapsedMilliseconds} ms] speech started");
        session.Transcribing += b => output.WriteLine($"  [{clock.ElapsedMilliseconds} ms] final transcribe {(b ? "begin" : "end")}");
        var done = new TaskCompletionSource();
        session.Preparing += m => { if (!m.Contains('%') || m.EndsWith("0%")) output.WriteLine(m); };
        session.Ready += () => { output.WriteLine("ready"); clock.Start(); };
        session.Final += t => { finals.Add(t); output.WriteLine($"[{clock.ElapsedMilliseconds} ms] {t}"); };
        session.Failed += m => output.WriteLine("FAILED " + m);
        session.Completed += () => done.TrySetResult();
        session.Start();

        // Wait for the model, then feed audio in real time (50 ms per chunk) like a microphone.
        var ready = new TaskCompletionSource();
        session.Ready += () => ready.TrySetResult();
        await ready.Task.WaitAsync(TimeSpan.FromMinutes(10));
        await Task.Delay(500);

        var bytes = pcm.ToArray();
        for (int i = 0; i + 1600 <= bytes.Length; i += 1600)
        {
            var chunk = bytes[i..(i + 1600)];
            double sum = 0;
            for (int j = 0; j < chunk.Length; j += 2) { double s = (short)(chunk[j] | (chunk[j + 1] << 8)) / 32768.0; sum += s * s; }
            session.Write(chunk, (float)Math.Sqrt(sum / 800));
            await Task.Delay(50);
        }
        session.Complete();
        await done.Task.WaitAsync(TimeSpan.FromMinutes(10));

        Assert.NotEmpty(previews);
        var all = string.Join(" ", finals);
        Assert.Contains("SpeechFlow", all, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("manager", all, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("meeting", finals[^1], StringComparison.OrdinalIgnoreCase);
    }
}
