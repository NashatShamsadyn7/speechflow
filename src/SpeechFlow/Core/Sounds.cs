using System;
using System.IO;
using System.Media;
using System.Text;

namespace SpeechFlow.Core;

/// <summary>Short soft chimes, synthesised once at startup and played asynchronously.</summary>
public sealed class Sounds
{
    const int Rate = 22050;

    readonly SoundPlayer _start = Build((660, 70), (880, 90));
    readonly SoundPlayer _stop = Build((880, 70), (587, 90));
    readonly SoundPlayer _switch = Build((990, 60));
    readonly SoundPlayer _error = Build((330, 140), (262, 160));

    public bool Enabled { get; set; } = true;

    public void Start() => Play(_start);
    public void Stop() => Play(_stop);
    public void Switch() => Play(_switch);
    public void Error() => Play(_error);

    void Play(SoundPlayer player)
    {
        if (!Enabled) return;
        try { player.Play(); }
        catch (Exception ex) { Log.Error("Sound playback failed", ex); }
    }

    static SoundPlayer Build(params (double Hz, int Ms)[] notes)
    {
        var pcm = new MemoryStream();
        var writer = new BinaryWriter(pcm);
        foreach (var (hz, ms) in notes)
        {
            int count = Rate * ms / 1000;
            int fade = Rate * 8 / 1000;
            for (int i = 0; i < count; i++)
            {
                double envelope = Math.Min(1.0, Math.Min(i, count - i) / (double)fade);
                double sample = Math.Sin(2 * Math.PI * hz * i / Rate) * envelope * 0.22;
                writer.Write((short)(sample * short.MaxValue));
            }
        }
        writer.Flush();

        var wav = new MemoryStream();
        var w = new BinaryWriter(wav);
        int dataLength = (int)pcm.Length;
        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataLength);
        w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        w.Write(16);
        w.Write((short)1);      // PCM
        w.Write((short)1);      // mono
        w.Write(Rate);
        w.Write(Rate * 2);      // byte rate
        w.Write((short)2);      // block align
        w.Write((short)16);     // bits per sample
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(dataLength);
        w.Write(pcm.ToArray());
        w.Flush();
        wav.Position = 0;

        var player = new SoundPlayer(wav);
        player.Load();
        return player;
    }
}
