using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Interop;
using static SpeechFlow.Core.NativeMethods;

namespace SpeechFlow.Core;

/// <summary>
/// System-wide hotkeys through RegisterHotKey. Unlike a low-level keyboard hook this needs no
/// admin rights, isn't flagged by antivirus, and WM_HOTKEY arrives on the UI thread.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    readonly HwndSource _window;
    readonly Dictionary<int, Action> _actions = new();
    int _nextId = 1;

    public HotkeyManager()
    {
        // Message-only window (HWND_MESSAGE): never visible, only receives WM_HOTKEY.
        _window = new HwndSource(new HwndSourceParameters("SpeechFlowHotkeys") { ParentWindow = new IntPtr(-3) });
        _window.AddHook(WndProc);
    }

    /// <returns>False if the text isn't a valid hotkey or another app already owns it.</returns>
    public bool Register(string gesture, Action action)
    {
        if (!HotkeyGesture.TryParse(gesture, out uint modifiers, out uint vk)) return false;
        int id = _nextId++;
        if (!RegisterHotKey(_window.Handle, id, modifiers | MOD_NOREPEAT, vk))
        {
            Log.Info($"Hotkey {gesture} is already in use by another app");
            return false;
        }
        _actions[id] = action;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _actions.Keys) UnregisterHotKey(_window.Handle, id);
        _actions.Clear();
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            action();
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _window.Dispose();
    }
}

public static class HotkeyGesture
{
    /// <summary>Parses text like "Ctrl+Shift+Space". At least one modifier is required.</summary>
    public static bool TryParse(string text, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        Key? key = null;

        foreach (var part in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= MOD_CONTROL; break;
                case "shift": modifiers |= MOD_SHIFT; break;
                case "alt": modifiers |= MOD_ALT; break;
                case "win" or "windows": modifiers |= MOD_WIN; break;
                default:
                    if (key is not null) return false;
                    var name = part.ToLowerInvariant() switch
                    {
                        "backspace" => "Back",
                        "esc" => "Escape",
                        "enter" => "Return",
                        _ => part.Length == 1 && char.IsDigit(part[0]) ? "D" + part : part,
                    };
                    if (!Enum.TryParse(name, ignoreCase: true, out Key parsed) || parsed == Key.None) return false;
                    key = parsed;
                    break;
            }
        }

        if (key is null || modifiers == 0) return false;
        virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key.Value);
        return virtualKey != 0;
    }
}
