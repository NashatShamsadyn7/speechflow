using System;
using System.Threading;
using System.Threading.Tasks;

namespace SpeechFlow.Core.Speech;

/// <summary>Turns one finished utterance (16 kHz mono PCM16) into text.</summary>
public interface ISpeechRecognizer
{
    /// <summary>Downloads/loads whatever is needed. Reports human-readable progress.</summary>
    Task PrepareAsync(IProgress<string> progress, CancellationToken ct);

    /// <param name="language">"ar", "en", or null to let the model detect it.</param>
    Task<string> TranscribeAsync(byte[] pcm16, string? language, CancellationToken ct);
}

/// <summary>A problem the user has to fix (bad key, no disk space…); stops the session.</summary>
public sealed class SpeechException(string message, Exception? inner = null) : Exception(message, inner);

static class Hallucinations
{
    // Whisper was trained on subtitled video, so on noise it sometimes "hears" subtitle credits.
    static readonly string[] Phrases =
    {
        "ترجمة نانسي قنقر", "اشتركوا في القناة", "اشترك في القناة", "نانسي قنقر",
        "thanks for watching", "thank you for watching", "subtitles by the amara.org community",
        "please subscribe", "you",
    };

    public static bool IsHallucination(string text)
    {
        var core = text.Trim().Trim('.', '!', '؟', '?', '،', ',', ' ').ToLowerInvariant();
        if (core.Length == 0) return true;
        foreach (var phrase in Phrases)
            if (core == phrase) return true;
        return false;
    }
}
