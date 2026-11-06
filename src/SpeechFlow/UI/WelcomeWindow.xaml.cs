using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SpeechFlow.Core;

namespace SpeechFlow.UI;

/// <summary>First-run setup: language → microphone test → engine and key → how to use it.</summary>
public partial class WelcomeWindow : Window
{
    const string DefaultMicrophone = "Windows default";
    const float HeardLevel = 0.03f;

    readonly DictationController _controller;
    readonly AudioCapture _micTest = new();
    readonly StackPanel[] _steps;
    readonly System.Windows.Shapes.Ellipse[] _dots;
    int _step;
    int _loudChunks;

    public WelcomeWindow(DictationController controller)
    {
        InitializeComponent();
        _controller = controller;
        _steps = new[] { Step0, Step1, Step2, Step3 };
        _dots = new[] { Dot0, Dot1, Dot2, Dot3 };

        MicrophoneBox.Items.Add(DefaultMicrophone);
        foreach (var name in AudioCapture.ListDevices()) MicrophoneBox.Items.Add(name);
        MicrophoneBox.SelectedIndex = 0;

        var hotkey = controller.Settings.ToggleHotkey;
        TapText.Text = $"Tap {hotkey} — start listening";
        HoldText.Text = $"Hold {hotkey} — push to talk";

        _micTest.ChunkAvailable += (_, rms) => Dispatcher.BeginInvoke(() => ShowLevel(rms));
        ShowStep(0);
    }

    void ShowStep(int step)
    {
        _step = step;
        for (int i = 0; i < _steps.Length; i++)
        {
            _steps[i].Visibility = i == step ? Visibility.Visible : Visibility.Collapsed;
            _dots[i].Fill = new SolidColorBrush(i <= step ? Color.FromRgb(14, 165, 233) : Color.FromRgb(229, 231, 235));
        }
        BackButton.Visibility = step is > 0 and < 3 ? Visibility.Visible : Visibility.Hidden;
        NextButton.Content = step == 3 ? "Start using SpeechFlow" : "Next";
        NextButton.Width = step == 3 ? 190 : 110;

        if (step == 1) StartMicTest();
        else _micTest.Stop();
    }

    void OnNext(object sender, RoutedEventArgs e)
    {
        if (_step == 2 && !ValidateEngine()) return;
        if (_step == 3)
        {
            Finish();
            return;
        }
        ShowStep(_step + 1);
    }

    void OnBack(object sender, RoutedEventArgs e) => ShowStep(Math.Max(0, _step - 1));

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

    void OnEngineChanged(object sender, RoutedEventArgs e)
    {
        if (KeyPanel is null) return; // fires during InitializeComponent
        KeyPanel.Visibility = CloudCard.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        EngineError.Text = "";
    }

    void OnGetKey(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://console.groq.com/keys") { UseShellExecute = true });

    bool ValidateEngine()
    {
        EngineError.Text = "";
        if (CloudCard.IsChecked != true) return true;
        var key = KeyBox.Password.Trim();
        if (key.Length == 0)
        {
            EngineError.Text = "Paste your key first, or choose “On this PC”.";
            return false;
        }
        if (!key.StartsWith("gsk_", StringComparison.Ordinal))
        {
            EngineError.Text = "That doesn't look like a Groq key (they start with gsk_). Copy it again.";
            return false;
        }
        return true;
    }

    string SelectedMicrophone =>
        MicrophoneBox.SelectedItem as string is { } mic && mic != DefaultMicrophone ? mic : "";

    void Finish()
    {
        var s = _controller.Settings.Clone();
        s.Language = ArabicCard.IsChecked == true ? "ar" : "en";
        s.Translate = false;
        s.Microphone = SelectedMicrophone;
        s.Engine = CloudCard.IsChecked == true ? "groq" : "local";
        if (s.Engine == "groq") s.GroqKey = KeyBox.Password.Trim();
        s.LivePreview = LivePreviewBox.IsChecked == true;
        s.Onboarded = true;
        _controller.ApplySettings(s, _controller.Snippets);
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _micTest.Dispose();
        base.OnClosing(e);
    }
}
