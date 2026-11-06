using System;
using Microsoft.Win32;

namespace SpeechFlow.Core;

public static class StartupRegistration
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "SpeechFlow";
    /// <summary>Passed when Windows starts the app, so it starts quietly (no "ready" notification).</summary>
    public const string StartupArgument = "--startup";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled && Environment.ProcessPath is { } exe)
                key.SetValue(ValueName, $"\"{exe}\" {StartupArgument}");
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            Log.Error("Updating the startup entry failed", ex);
        }
    }
}
