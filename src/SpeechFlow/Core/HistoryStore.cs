using System;
using System.IO;
using System.Text.Json;

namespace SpeechFlow.Core;

/// <summary>Optional local history, one JSON line per dictation session. Off by default.</summary>
public static class HistoryStore
{
    public static void Append(string text, string language)
    {
        try
        {
            AppPaths.EnsureDataDir();
            var line = JsonSerializer.Serialize(new { time = DateTime.Now.ToString("s"), language, text });
            File.AppendAllText(AppPaths.HistoryFile, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Log.Error("Writing history failed", ex);
        }
    }

    public static void Clear()
    {
        try { File.Delete(AppPaths.HistoryFile); }
        catch (Exception ex) { Log.Error("Clearing history failed", ex); }
    }
}
