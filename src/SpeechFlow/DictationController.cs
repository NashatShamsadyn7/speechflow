using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using SpeechFlow.Core;

namespace SpeechFlow;

public enum StatusKind { Info, Busy, Ok, Error }

/// <summary>
/// The app's brain. Every public member and every event runs on the UI thread; work coming from
/// audio, network or hotkeys is marshalled here first, so the UI is never touched off-thread.
/// </summary>
public sealed class DictationController : IDisposable
{
    readonly Dispatcher _ui;
    readonly AudioCapture _audio = new();
    readonly Typer _typer = new();
    readonly Sounds _sounds = new();
    readonly TextFormatter _formatter = new();
    readonly DispatcherTimer _autoStop;
    HotkeyManager? _hotkeys;

    volatile DeepgramStream? _stream;
    DateTime _lastActivity;
    volatile float _level;

    // What we typed since dictation last started, segment by segment, for undo and polish.
    readonly List<string> _segments = new();
    char? _lastTypedChar;
    IntPtr _lastTargetWindow;
    bool _polishing;

    public AppSettings Settings { get; private set; }
    public List<Snippet> Snippets { get; private set; }
    public bool IsListening { get; private set; }
    /// <summary>Latest microphone level (0..1); safe to read from any thread.</summary>
    public float Level => _level;

    public event Action? StateChanged;
    public event Action<string, StatusKind>? StatusChanged;
    public event Action<string>? PreviewChanged;
    public event Action? SettingsRequested;
    public event Action<string>? Notify;

    public DictationController(Dispatcher ui)
    {
        _ui = ui;
        Settings = SettingsStore.Load();
        Snippets = SnippetStore.Load();
        ApplyToComponents();

        _audio.ChunkAvailable += OnAudioChunk;
        _audio.Failed += message => _ui.BeginInvoke(() => Fail(message));

        _autoStop = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => CheckAutoStop(), _ui);
        _autoStop.Start();
    }

    /// <summary>Call once the UI exists so hotkey problems can be shown to the user.</summary>
    public void Initialize()
    {
        _hotkeys = new HotkeyManager();
        RegisterHotkeys();
        SetIdleStatus();
    }

    public void RequestSettings() => SettingsRequested?.Invoke();

    public void Toggle()
    {
        if (IsListening) Stop();
        else Start();
    }

    public void Start(bool quiet = false)
    {
        if (IsListening) return;
        if (string.IsNullOrWhiteSpace(Settings.DeepgramKey))
        {
            Status("Add your free Deepgram API key to start.", StatusKind.Error);
            SettingsRequested?.Invoke();
            return;
        }

        var language = Settings.Language;
        var stream = new DeepgramStream(Settings.DeepgramKey, Settings.DeepgramLanguage, Settings.Model);
        var sessionText = new StringBuilder();
        stream.Interim += text => _ui.BeginInvoke(() => OnInterim(stream, text));
        stream.Final += text => _ui.BeginInvoke(() => OnFinal(stream, text, language, sessionText));
        stream.StatusChanged += status => _ui.BeginInvoke(() => OnStreamStatus(stream, status));
        stream.Failed += message => _ui.BeginInvoke(() => { if (stream == _stream) Fail(message); });
        stream.Completed += () => _ui.BeginInvoke(() => OnSessionCompleted(sessionText, language));

        _stream = stream;
        try
        {
            _audio.Start(Settings.Microphone);
        }
        catch (Exception ex)
        {
            _stream = null;
            Log.Error("Could not open the microphone", ex);
            Fail("Can't open the microphone. Check Windows Settings → Privacy → Microphone.");
            return;
        }
        stream.Start();

        IsListening = true;
        _segments.Clear();
        _lastActivity = DateTime.UtcNow;
        if (!quiet) _sounds.Start();
        PreviewChanged?.Invoke("");
        StateChanged?.Invoke();
    }

    public void Stop(bool quiet = false)
    {
        if (!IsListening) return;
        IsListening = false;
        _audio.Stop();
        _stream?.Complete(); // keeps delivering the last words for a moment
        _stream = null;
        _level = 0;
        if (!quiet) _sounds.Stop();
        StateChanged?.Invoke();
        SetIdleStatus();
    }

    public void SwitchLanguage()
    {
        Settings.Language = Settings.Language == "ar" ? "en" : "ar";
        Save();
        _sounds.Switch();
        if (IsListening)
        {
            // A Deepgram stream has one language, so restart; the old one still flushes its last words.
            Stop(quiet: true);
            Start(quiet: true);
        }
        StateChanged?.Invoke();
        if (!IsListening) SetIdleStatus();
    }

    /// <summary>Deletes the last typed phrase. Repeat to go further back.</summary>
    public void UndoLast()
    {
        if (_segments.Count == 0)
        {
            _sounds.Error();
            Status("Nothing to undo.", StatusKind.Info);
            return;
        }
        var last = _segments[^1];
        _segments.RemoveAt(_segments.Count - 1);
        _typer.Backspace(TextLength(last));
        _lastTypedChar = _segments.Count > 0 ? _segments[^1][^1] : null;
        Status("Removed the last phrase.", StatusKind.Ok);
    }

    /// <summary>Replaces the last dictation with an AI-polished version.</summary>
    public async void PolishLast()
    {
        if (_polishing) return;
        if (IsListening) Stop();
        if (string.IsNullOrWhiteSpace(Settings.GroqKey))
        {
            Status("Add a Groq API key in Settings to use AI polish.", StatusKind.Error);
            return;
        }
        if (_segments.Count == 0)
        {
            Status("Dictate something first.", StatusKind.Info);
            return;
        }

        var original = string.Concat(_segments);
        var snapshot = _segments.Count;
        _polishing = true;
        Status("Polishing with AI…", StatusKind.Busy);
        try
        {
            var polished = await GroqPolisher.PolishAsync(Settings.GroqKey, original.Trim(), Settings.PolishMode);
            if (_segments.Count != snapshot || IsListening)
            {
                Status("Skipped polish: you dictated again meanwhile.", StatusKind.Info);
                return;
            }
            if (polished.Length == 0) return;

            var replacement = (original.StartsWith(' ') ? " " : "") + polished;
            _typer.Backspace(TextLength(original));
            _typer.Type(replacement, Settings.TypingMode == "paste");
            _segments.Clear();
            _segments.Add(replacement);
            _lastTypedChar = replacement[^1];
            Status("Polished.", StatusKind.Ok);
        }
        catch (Exception ex)
        {
            Log.Error("AI polish failed", ex);
            Fail(ex is InvalidOperationException ? ex.Message : "AI polish failed. Check your internet connection.");
        }
        finally
        {
            _polishing = false;
        }
    }

    /// <returns>Hotkeys that couldn't be registered (invalid or taken by another app).</returns>
    public IReadOnlyList<string> ApplySettings(AppSettings settings, List<Snippet> snippets)
    {
        bool wasListening = IsListening;
        if (wasListening) Stop(quiet: true);

        Settings = settings;
        Snippets = snippets;
        Save();
        SnippetStore.Save(snippets);
        StartupRegistration.Apply(settings.StartWithWindows);
        ApplyToComponents();
        var failed = RegisterHotkeys();

        StateChanged?.Invoke();
        if (wasListening) Start(quiet: true);
        else SetIdleStatus();
        return failed;
    }

    public void SaveBarPosition(double left, double top)
    {
        Settings.BarLeft = left;
        Settings.BarTop = top;
        Save();
    }

    void OnAudioChunk(byte[] pcm, float rms)
    {
        _level = rms;
        _stream?.Write(pcm);
    }

    void OnInterim(DeepgramStream stream, string text)
    {
        if (stream != _stream) return;
        _lastActivity = DateTime.UtcNow;
        PreviewChanged?.Invoke(text);
    }

    void OnFinal(DeepgramStream stream, string text, string language, StringBuilder sessionText)
    {
        if (stream == _stream) _lastActivity = DateTime.UtcNow;

        // If the user moved to another window, we don't know what's before the cursor any more.
        var target = Core.NativeMethods.GetForegroundWindow();
        if (target != _lastTargetWindow)
        {
            _lastTargetWindow = target;
            _lastTypedChar = null;
            _segments.Clear();
        }

        var output = _formatter.Format(text, language, _lastTypedChar);
        if (output.Length == 0) return;

        _typer.Type(output, Settings.TypingMode == "paste");
        _segments.Add(output);
        _lastTypedChar = output[^1];
        sessionText.Append(output);
        PreviewChanged?.Invoke(output.Trim());
    }

    void OnStreamStatus(DeepgramStream stream, StreamStatus status)
    {
        if (stream != _stream || !IsListening) return;
        switch (status)
        {
            case StreamStatus.Connecting: Status("Connecting…", StatusKind.Busy); break;
            case StreamStatus.Reconnecting: Status("Connection lost, reconnecting…", StatusKind.Busy); break;
            case StreamStatus.Connected: Status(Settings.Language == "ar" ? "Listening — تكلّم الآن" : "Listening — speak now", StatusKind.Ok); break;
        }
    }

    void OnSessionCompleted(StringBuilder sessionText, string language)
    {
        if (Settings.SaveHistory && sessionText.Length > 0)
            HistoryStore.Append(sessionText.ToString().Trim(), language);
    }

    void CheckAutoStop()
    {
        if (!IsListening || Settings.AutoStopSeconds <= 0) return;
        if ((DateTime.UtcNow - _lastActivity).TotalSeconds >= Settings.AutoStopSeconds)
        {
            Stop();
            Status($"Stopped after {Settings.AutoStopSeconds}s of silence.", StatusKind.Info);
        }
    }

    void Fail(string message)
    {
        if (IsListening)
        {
            IsListening = false;
            _audio.Stop();
            _stream?.Complete();
            _stream = null;
            _level = 0;
            StateChanged?.Invoke();
        }
        _sounds.Error();
        Status(message, StatusKind.Error);
        Notify?.Invoke(message);
    }

    IReadOnlyList<string> RegisterHotkeys()
    {
        var failed = new List<string>();
        if (_hotkeys is null) return failed;
        _hotkeys.UnregisterAll();
        void Add(string gesture, Action action)
        {
            if (string.IsNullOrWhiteSpace(gesture)) return;
            if (!_hotkeys.Register(gesture, action)) failed.Add(gesture);
        }
        Add(Settings.ToggleHotkey, Toggle);
        Add(Settings.LanguageHotkey, SwitchLanguage);
        Add(Settings.UndoHotkey, UndoLast);
        Add(Settings.PolishHotkey, PolishLast);
        if (failed.Count > 0)
            Notify?.Invoke("These shortcuts are used by another app: " + string.Join(", ", failed));
        return failed;
    }

    void ApplyToComponents()
    {
        _sounds.Enabled = Settings.Sounds;
        _formatter.VoiceCommands = Settings.VoiceCommands;
        _formatter.StripTashkeel = Settings.StripTashkeel;
        _formatter.SetSnippets(Snippets);
    }

    void SetIdleStatus() => Status($"Press {Settings.ToggleHotkey} to dictate", StatusKind.Info);

    void Status(string text, StatusKind kind) => StatusChanged?.Invoke(text, kind);

    void Save()
    {
        try { SettingsStore.Save(Settings); }
        catch (Exception ex) { Log.Error("Saving settings failed", ex); }
    }

    /// <summary>Backspaces needed to delete text: one per user-perceived character.</summary>
    static int TextLength(string text) => new StringInfo(text).LengthInTextElements;

    public void Dispose()
    {
        _autoStop.Stop();
        if (IsListening) Stop(quiet: true);
        _audio.Dispose();
        _hotkeys?.Dispose();
        _typer.Dispose();
    }
}
