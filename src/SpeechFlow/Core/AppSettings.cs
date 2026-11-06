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
    /// <summary>Show words on the bar while speaking, using the local model (downloaded once).</summary>
    public bool LivePreview { get; set; } = true;

    /// <summary>"ar", "en" or "auto".</summary>
    public string Language { get; set; } = "ar";
    /// <summary>Translate what's said into English instead of typing it as spoken.</summary>
    public bool Translate { get; set; }
    /// <summary>Type Arabic text with Arabic-Indic digits (٣٥ rather than 35).</summary>
    public bool ArabicDigits { get; set; } = true;
    /// <summary>False until the welcome screen has been completed.</summary>
    public bool Onboarded { get; set; }

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

    // Only the toggle is on by default: every global hotkey is stolen from all other apps
    // (Ctrl+Shift+L is Excel's filter and VS Code's select-all-matches, for example).
    public string ToggleHotkey { get; set; } = "Ctrl+Shift+Space";
    public string LanguageHotkey { get; set; } = "";
    public string UndoHotkey { get; set; } = "";
    public string PolishHotkey { get; set; } = "";
    /// <summary>Set once the pre-3.1 default hotkeys have been cleared from an older settings file.</summary>
    public bool LegacyHotkeysCleared { get; set; }

    public DateTime? LastUpdateCheck { get; set; }

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
                if (ClearLegacyHotkeys(settings)) Save(settings);
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

    /// <summary>3.0 shipped extra default hotkeys that clash with Excel and VS Code; drop them unless the user changed them.</summary>
    /// <returns>True if the settings changed and should be saved.</returns>
    static bool ClearLegacyHotkeys(AppSettings s)
    {
        if (s.LegacyHotkeysCleared) return false;
        if (s.LanguageHotkey == "Ctrl+Shift+L") s.LanguageHotkey = "";
        if (s.UndoHotkey == "Ctrl+Shift+Backspace") s.UndoHotkey = "";
        if (s.PolishHotkey == "Ctrl+Shift+K") s.PolishHotkey = "";
        s.LegacyHotkeysCleared = true;
        return true;
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
