using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace SpeechFlow.Core;

/// <summary>Microphone → 16 kHz mono 16-bit PCM in 50 ms chunks. Windows does the resampling.</summary>
public sealed class AudioCapture : IDisposable
{
    public const int SampleRate = 16000;
    const int DefaultDevice = -1; // WAVE_MAPPER

    WaveInEvent? _wave;

    /// <summary>PCM chunk and its RMS level (0..1). Raised on an audio thread.</summary>
    public event Action<byte[], float>? ChunkAvailable;
    public event Action<string>? Failed;

    public static IReadOnlyList<string> ListDevices()
    {
        var names = new List<string>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            names.Add(WaveInEvent.GetCapabilities(i).ProductName);
        return names;
    }

    /// <summary>Throws if the microphone can't be opened (missing, in use, or blocked by privacy settings).</summary>
    public void Start(string deviceName)
    {
        Stop();
        var wave = new WaveInEvent
        {
            DeviceNumber = FindDevice(deviceName),
            WaveFormat = new WaveFormat(SampleRate, 16, 1),
            BufferMilliseconds = 50,
            NumberOfBuffers = 3,
        };
        wave.DataAvailable += (_, e) =>
        {
            if (e.BytesRecorded == 0) return;
            var chunk = new byte[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, chunk, 0, e.BytesRecorded);
            ChunkAvailable?.Invoke(chunk, Rms(chunk));
        };
        wave.RecordingStopped += (_, e) =>
        {
            if (e.Exception is not null)
            {
                Log.Error("Microphone stopped unexpectedly", e.Exception);
                Failed?.Invoke("The microphone stopped working.");
            }
        };
        wave.StartRecording();
        _wave = wave;
    }

    public void Stop()
    {
        var wave = _wave;
        _wave = null;
        if (wave is null) return;
        try { wave.StopRecording(); }
        catch (Exception ex) { Log.Error("Stopping the microphone failed", ex); }
        wave.Dispose();
    }

    static int FindDevice(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return DefaultDevice;
        var devices = ListDevices();
        for (int i = 0; i < devices.Count; i++)
            if (string.Equals(devices[i], name, StringComparison.OrdinalIgnoreCase)) return i;
        return DefaultDevice; // the saved mic was unplugged; fall back instead of failing
    }

    static float Rms(byte[] pcm)
    {
        int samples = pcm.Length / 2;
        if (samples == 0) return 0;
        double sum = 0;
        for (int i = 0; i < pcm.Length - 1; i += 2)
        {
            double s = (short)(pcm[i] | (pcm[i + 1] << 8)) / 32768.0;
            sum += s * s;
        }
        return (float)Math.Sqrt(sum / samples);
    }

    public void Dispose() => Stop();
}
