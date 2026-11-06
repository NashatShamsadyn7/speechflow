using System;
using System.IO;
using System.Text;

namespace SpeechFlow.Core;

/// <summary>
/// All user data lives in %APPDATA%\SpeechFlow, never next to the exe
/// (Program Files is read-only and single-file builds extract to a temp folder).
/// </summary>
public static class AppPaths
{
    public static string DataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpeechFlow");

    public static string SettingsFile => Path.Combine(DataDir, "settings.json");
    public static string SnippetsFile => Path.Combine(DataDir, "snippets.json");
    public static string HistoryFile => Path.Combine(DataDir, "history.jsonl");
    public static string LogFile => Path.Combine(DataDir, "speechflow.log");

    public static void EnsureDataDir() => Directory.CreateDirectory(DataDir);

    /// <summary>Writes via a temp file so a crash mid-write never leaves a half-written file.</summary>
    public static void WriteAllTextAtomic(string path, string content)
    {
        EnsureDataDir();
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }
}
