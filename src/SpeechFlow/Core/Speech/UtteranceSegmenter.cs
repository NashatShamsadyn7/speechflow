using System;
using System.Collections.Generic;
using System.IO;

namespace SpeechFlow.Core.Speech;

/// <summary>
/// Splits the microphone stream into utterances at natural pauses, so each phrase is typed as
/// soon as you stop for a breath. Uses an energy gate that adapts to the room's noise level.
/// </summary>
public sealed class UtteranceSegmenter
{
    const int BytesPerMs = AudioCapture.SampleRate * 2 / 1000;
    const int PreRollMs = 300;          // keep the quiet start of the first word
    const int EndPauseMs = 700;         // this much silence ends a phrase
    const int SoftLimitMs = 12_000;     // after this, any short pause ends it
    const int SoftPauseMs = 250;
    const int HardLimitMs = 25_000;     // never send more than this in one go
    const int MinSpeechMs = 250;        // shorter blips are coughs and clicks
    const float MinThreshold = 0.008f;

    readonly Queue<byte[]> _preRoll = new();
    int _preRollMs;
    readonly MemoryStream _current = new();
    int _speechMs, _silenceMs, _lengthMs;
    float _noiseFloor = 0.003f;

    public bool InSpeech { get; private set; }

    /// <summary>Length of the phrase in progress, in milliseconds.</summary>
    public int LengthMs => InSpeech ? _lengthMs : 0;

    /// <summary>Copy of the phrase in progress, for a live preview.</summary>
    public byte[] Snapshot() => _current.ToArray();

    /// <returns>A finished utterance, or null.</returns>
    public byte[]? Add(byte[] chunk, float rms)
    {
        int ms = chunk.Length / BytesPerMs;
        bool voiced = rms > Math.Max(MinThreshold, _noiseFloor * 3);
        if (!voiced) _noiseFloor = _noiseFloor * 0.95f + rms * 0.05f;

        if (!InSpeech)
        {
            if (!voiced)
            {
                _preRoll.Enqueue(chunk);
                _preRollMs += ms;
                while (_preRollMs > PreRollMs) _preRollMs -= _preRoll.Dequeue().Length / BytesPerMs;
                return null;
            }
            InSpeech = true;
            foreach (var c in _preRoll) _current.Write(c);
            _lengthMs = _preRollMs;
            _preRoll.Clear();
            _preRollMs = 0;
        }

        _current.Write(chunk);
        _lengthMs += ms;
        if (voiced)
        {
            _speechMs += ms;
            _silenceMs = 0;
        }
        else
        {
            _silenceMs += ms;
        }

        bool ended = _silenceMs >= EndPauseMs
            || (_lengthMs >= SoftLimitMs && _silenceMs >= SoftPauseMs)
            || _lengthMs >= HardLimitMs;
        return ended ? Cut() : null;
    }

    /// <summary>Returns whatever speech is in progress (used when dictation stops).</summary>
    public byte[]? Flush() => InSpeech ? Cut() : null;

    byte[]? Cut()
    {
        var data = _current.ToArray();
        bool enoughSpeech = _speechMs >= MinSpeechMs;
        _current.SetLength(0);
        _speechMs = _silenceMs = _lengthMs = 0;
        InSpeech = false;
        return enoughSpeech ? data : null;
    }
}
