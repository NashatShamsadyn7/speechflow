using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SpeechFlow.Core;
using SpeechFlow.Core.Speech;

namespace SpeechFlow.UI;

/// <summary>
/// First-run setup: language → microphone test → engine → (cloud only) key → how to use it.
/// On the key step the clipboard is watched, so copying the key on Groq's page is all it takes.
/// </summary>
public partial class WelcomeWindow : Window
{
    const string DefaultMicrophone = "Windows default";
    const float HeardLevel = 0.03f;
    static readonly Regex KeyPattern = new(@"^gsk_[A-Za-z0-9]{20,}$");

    readonly DictationController _controller;
    readonly AudioCapture _micTest = new();
    readonly StackPanel[] _steps;
    readonly DispatcherTimer _clipboardWatch;
    int _step;
    int _loudChunks;
    string _checkedKey = "";
    bool _keyValid;

    /// <summary>Raised after setup is saved.</summary>
    public event Action? Finished;

    public WelcomeWindow(DictationController controller)
    {
        InitializeComponent();
        _controller = controller;
        // Order of steps; StepKey is skipped when the on-device engine is chosen.
        _steps = new[] { Step0, Step1, Step2, StepKey, Step3 };

        MicrophoneBox.Items.Add(DefaultMicrophone);
        foreach (var name in AudioCapture.ListDevices()) MicrophoneBox.Items.Add(name);
        MicrophoneBox.SelectedIndex = 0;

        var hotkey = controller.Settings.ToggleHotkey;
        TapText.Text = $"Tap {hotkey} — start listening";
        HoldText.Text = $"Hold {hotkey} — push to talk";

        _micTest.ChunkAvailable += (_, rms) => Dispatcher.BeginInvoke(() => ShowLevel(rms));
        _clipboardWatch = new DispatcherTimer(TimeSpan.FromMilliseconds(700), DispatcherPriority.Background, (_, _) => WatchClipboard(), Dispatcher);
        ShowStep(0);
    }

    bool UsesCloud => CloudCard.IsChecked == true;

    void ShowStep(int step)
    {
        _step = step;
        for (int i = 0; i < _steps.Length; i++)
            _steps[i].Visibility = i == step ? Visibility.Visible : Visibility.Collapsed;

        // Four dots: the key step shares a dot with the engine choice.
        int dot = step switch { 0 => 0, 1 => 1, 2 or 3 => 2, _ => 3 };
        var dots = new[] { Dot0, Dot1, Dot2, Dot3 };
        for (int i = 0; i < dots.Length; i++)
            dots[i].Fill = new SolidColorBrush(i <= dot ? Color.FromRgb(56, 189, 248) : Color.FromRgb(55, 65, 81));

        bool last = step == _steps.Length - 1;
        BackButton.Visibility = step > 0 && !last ? Visibility.Visible : Visibility.Hidden;
        NextButton.Content = last ? "Start using SpeechFlow" : "Next";
        NextButton.IsEnabled = step != 3 || _keyValid;

        if (step == 1) StartMicTest();
        else _micTest.Stop();

        if (step == 3)
        {
            _clipboardWatch.Start();
            WatchClipboard();
        }
        else
        {
            _clipboardWatch.Stop();
        }
    }

    void OnNext(object sender, RoutedEventArgs e)
    {
        switch (_step)
        {
            case 2: ShowStep(UsesCloud ? 3 : 4); break;
            case 4: Finish(); break;
            default: ShowStep(_step + 1); break;
        }
    }

    void OnBack(object sender, RoutedEventArgs e) =>
        ShowStep(_step == 4 && !UsesCloud ? 2 : Math.Max(0, _step - 1));

    void StartMicTest()
    {
        _loudChunks = 0;
        MicStatus.Text = "Waiting for your voice…";
        try
        {
            _micTest.Start(SelectedMicrophone);
        }
        catch (Exception ex)
        {
            Log.Error("Microphone test failed", ex);
            MicStatus.Text = "Can't open this microphone. Pick another one, or check Windows Settings → Privacy → Microphone.";
        }
    }

    void OnMicrophoneChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_step == 1) StartMicTest();
    }

    void ShowLevel(float rms)
    {
        MicLevel.Value = Math.Min(1.0, rms * 7);
        if (rms > HeardLevel && ++_loudChunks == 6)
            MicStatus.Text = "✓ We can hear you. Sounds good!";
    }

    void OnGetKey(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://console.groq.com/keys") { UseShellExecute = true });

    /// <summary>Picks up a Groq key as soon as it lands on the clipboard.</summary>
    void WatchClipboard()
    {
        string text;
        try
        {
            if (!Clipboard.ContainsText()) return;
            text = Clipboard.GetText().Trim();
        }
        catch (Exception)
        {
            return; // another app is holding the clipboard; try again on the next tick
        }
        if (KeyPattern.IsMatch(text) && text != KeyBox.Password) KeyBox.Password = text;
    }

    async void OnKeyChanged(object sender, RoutedEventArgs e)
    {
        var key = KeyBox.Password.Trim();
        _keyValid = false;
        NextButton.IsEnabled = false;
        if (key.Length == 0)
        {
            ShowKeyStatus("Waiting for you to copy the key…", "#9CA3AF");
            return;
        }
        if (!KeyPattern.IsMatch(key))
        {
            ShowKeyStatus("That doesn't look like a Groq key (they start with gsk_). Copy it again.", "#F87171");
            return;
        }

        _checkedKey = key;
        ShowKeyStatus("Checking the key…", "#9CA3AF");
        bool? valid;
        try { valid = await GroqRecognizer.CheckKeyAsync(key); }
        catch (Exception ex)
        {
            Log.Error("Key check failed", ex);
            valid = null;
        }
        if (key != _checkedKey) return; // the user changed it while we were checking

        switch (valid)
        {
            case true:
                _keyValid = true;
                NextButton.IsEnabled = _step == 3;
                ShowKeyStatus("✓ The key works. Click Next.", "#4ADE80");
                break;
            case false:
                ShowKeyStatus("Groq says this key isn't valid. Create a new one and copy it.", "#F87171");
                break;
            default:
                // Offline right now: accept it; a wrong key would be reported on first use.
                _keyValid = true;
                NextButton.IsEnabled = _step == 3;
                ShowKeyStatus("Couldn't reach Groq to check the key (no internet?). You can continue.", "#FBBF24");
                break;
        }
    }

    void ShowKeyStatus(string text, string color)
    {
        KeyStatus.Text = text;
        KeyStatus.Foreground = (Brush)new BrushConverter().ConvertFrom(color)!;
    }

    string SelectedMicrophone =>
        MicrophoneBox.SelectedItem as string is { } mic && mic != DefaultMicrophone ? mic : "";

    void Finish()
    {
        var s = _controller.Settings.Clone();
        s.Language = ArabicCard.IsChecked == true ? "ar" : "en";
        s.Translate = false;
        s.Microphone = SelectedMicrophone;
        s.Engine = UsesCloud ? "groq" : "local";
        if (UsesCloud) s.GroqKey = KeyBox.Password.Trim();
        s.LivePreview = LivePreviewBox.IsChecked == true;
        s.Onboarded = true;
        _controller.ApplySettings(s, _controller.Snippets); // also starts the model download if needed
        Finished?.Invoke();
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _clipboardWatch.Stop();
        _micTest.Dispose();
        base.OnClosing(e);
    }
}
