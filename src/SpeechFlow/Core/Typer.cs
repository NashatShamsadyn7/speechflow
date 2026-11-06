using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using static SpeechFlow.Core.NativeMethods;

namespace SpeechFlow.Core;

/// <summary>
/// Types into whatever window has focus. Work runs on its own STA thread (the clipboard needs STA)
/// so typing never blocks speech recognition or the UI.
/// </summary>
public sealed class Typer : IDisposable
{
    const int MaxInputsPerCall = 400;

    readonly BlockingCollection<Action> _queue = new();
    readonly Thread _thread;

    public Typer()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "SpeechFlow typer" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public void Type(string text, bool usePaste) => Enqueue(() =>
    {
        if (usePaste) Paste(text);
        else SendUnicode(text);
    });

    public void Backspace(int count) => Enqueue(() =>
    {
        var inputs = new List<INPUT>(count * 2);
        for (int i = 0; i < count; i++) AddKey(inputs, VK_BACK);
        Send(inputs);
    });

    void Enqueue(Action action)
    {
        if (_queue.IsAddingCompleted) return;
        _queue.Add(() =>
        {
            // The user may still be holding the hotkey's Ctrl/Shift; injected keys would combine with them.
            WaitForModifierRelease();
            action();
        });
    }

    void Run()
    {
        foreach (var action in _queue.GetConsumingEnumerable())
        {
            try { action(); }
            catch (Exception ex) { Log.Error("Typing failed", ex); }
        }
    }

    /// <summary>The whole string goes out in a few SendInput calls instead of one sleep per character.</summary>
    static void SendUnicode(string text)
    {
        var inputs = new List<INPUT>(text.Length * 2);
        foreach (char c in text)
        {
            if (c == '\r') continue;
            if (c == '\n')
            {
                // Shift+Enter: a line break in editors, and it doesn't send the message in chat apps.
                inputs.Add(Key(VK_SHIFT, 0));
                AddKey(inputs, VK_RETURN);
                inputs.Add(Key(VK_SHIFT, KEYEVENTF_KEYUP));
                continue;
            }
            // Surrogate pairs arrive as two UTF-16 units, which is what KEYEVENTF_UNICODE expects.
            inputs.Add(Unicode(c, 0));
            inputs.Add(Unicode(c, KEYEVENTF_KEYUP));
        }
        Send(inputs);
    }

    static void Paste(string text)
    {
        string? previous = null;
        Retry(() => { if (Clipboard.ContainsText()) previous = Clipboard.GetText(); });

        var data = new DataObject();
        data.SetText(text, TextDataFormat.UnicodeText);
        // Keep dictated text out of Win+V history and the cloud clipboard.
        data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(new byte[4]));
        data.SetData("CanIncludeInClipboardHistory", new MemoryStream(BitConverter.GetBytes(0)));
        data.SetData("CanUploadToCloudClipboard", new MemoryStream(BitConverter.GetBytes(0)));
        Retry(() => Clipboard.SetDataObject(data, copy: true));

        var inputs = new List<INPUT>
        {
            Key(VK_CONTROL, 0), Key(VK_V, 0), Key(VK_V, KEYEVENTF_KEYUP), Key(VK_CONTROL, KEYEVENTF_KEYUP),
        };
        Send(inputs);

        // Apps read the clipboard asynchronously; restoring too early pastes the old content.
        Thread.Sleep(400);
        if (previous is not null) Retry(() => Clipboard.SetText(previous));
    }

    static void Send(List<INPUT> inputs)
    {
        int size = Marshal.SizeOf<INPUT>();
        for (int start = 0; start < inputs.Count; start += MaxInputsPerCall)
        {
            var batch = inputs.GetRange(start, Math.Min(MaxInputsPerCall, inputs.Count - start)).ToArray();
            uint sent = SendInput((uint)batch.Length, batch, size);
            if (sent != batch.Length)
            {
                // Typically UIPI: the target window runs as administrator and we don't.
                Log.Error($"SendInput delivered {sent}/{batch.Length} inputs (error {Marshal.GetLastWin32Error()})");
                return;
            }
            if (start + MaxInputsPerCall < inputs.Count) Thread.Sleep(5);
        }
    }

    /// <summary>
    /// In push-to-talk the user holds Ctrl+Shift while speaking, so text waits until they let go;
    /// the cap only guards against a stuck key.
    /// </summary>
    static void WaitForModifierRelease()
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < 30_000 && AnyModifierDown()) Thread.Sleep(15);
    }

    static bool AnyModifierDown() =>
        IsDown(VK_CONTROL) || IsDown(VK_SHIFT) || IsDown(VK_MENU) || IsDown(VK_LWIN) || IsDown(VK_RWIN);

    static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    static void Retry(Action action)
    {
        for (int attempt = 1; ; attempt++)
        {
            try { action(); return; }
            catch (Exception ex) when (ex is ExternalException && attempt < 5) { Thread.Sleep(50); }
            catch (Exception ex) { Log.Error("Clipboard access failed", ex); return; }
        }
    }

    static void AddKey(List<INPUT> inputs, ushort vk)
    {
        inputs.Add(Key(vk, 0));
        inputs.Add(Key(vk, KEYEVENTF_KEYUP));
    }

    static INPUT Key(ushort vk, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = flags } },
    };

    static INPUT Unicode(char c, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion { ki = new KEYBDINPUT { wScan = c, dwFlags = KEYEVENTF_UNICODE | flags } },
    };

    public void Dispose() => _queue.CompleteAdding();
}
