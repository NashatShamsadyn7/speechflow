using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SpeechFlow.Core;

public sealed class AppSettings
{
    public string GroqKey { get; set; } = "";

    /// <summary>"groq" (cloud, nothing to download) or "local" (Whisper on this PC, offline).</summary>
    public string Engine { get; set; } = "groq";
    /// <summary>Id from <see cref="Speech.ModelStore.Models"/>, used by the local engine.</summary>
    public string LocalModel { get; set; } = "small";

    /// <summary>"ar", "en" or "auto".</summary>
    public string Language { get; set; } = "ar";

    /// <summary>"type" (SendInput) or "paste" (clipboard + Ctrl+V).</summary>
    public string TypingMode { get; set; } = "type";
    /// <summary>Microphone product name; empty means the Windows default device.</summary>
    public string Microphone { get; set; } = "";
    /// <summary>Stop listening after this many seconds without any recognised words; 0 disables.</summary>
    public int AutoStopSeconds { get; set; } = 15;

    public bool Sounds { get; set; } = true;
    public bool VoiceCommands { get; set; } = true;
    public bool StripTashkeel { get; set; }
    public bool SaveHistory { get; set; }
    public bool StartWithWindows { get; set; }

    public string PolishMode { get; set; } = "grammar";

    public string ToggleHotkey { get; set; } = "Ctrl+Shift+Space";
    public string LanguageHotkey { get; set; } = "Ctrl+Shift+L";
    public string UndoHotkey { get; set; } = "Ctrl+Shift+Backspace";
    public string PolishHotkey { get; set; } = "Ctrl+Shift+K";

    public double? BarLeft { get; set; }
    public double? BarTop { get; set; }

    /// <summary>Language passed to the recognizer; null lets Whisper detect it per phrase.</summary>
    public string? RecognizerLanguage => Language == "auto" ? null : Language;

    public AppSettings Clone() =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this))!;
}

/// <summary>
/// Loads and saves settings. API keys are encrypted with DPAPI for the current Windows user,
/// so copying settings.json to another machine or account does not leak them.
/// </summary>
public static class SettingsStore
{
    const string ProtectedPrefix = "dpapi:";
    static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SpeechFlow.v3");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile)) ?? new();
                settings.GroqKey = Unprotect(settings.GroqKey);
                return settings;
            }
        }
        catch (Exception ex)
        {
            Log.Error("Settings file unreadable, using defaults", ex);
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        var node = JsonSerializer.SerializeToNode(settings)!.AsObject();
        node[nameof(AppSettings.GroqKey)] = Protect(settings.GroqKey);
        AppPaths.WriteAllTextAtomic(AppPaths.SettingsFile, node.ToJsonString(Json));
    }

    static string Protect(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser);
        return ProtectedPrefix + Convert.ToBase64String(bytes);
    }

    static string Unprotect(string value)
    {
        if (!value.StartsWith(ProtectedPrefix, StringComparison.Ordinal)) return value;
        try
        {
            var bytes = Convert.FromBase64String(value[ProtectedPrefix.Length..]);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes, Entropy, DataProtectionScope.CurrentUser));
        }
        catch (Exception ex)
        {
            Log.Error("Stored API key could not be decrypted", ex);
            return "";
        }
    }
}
