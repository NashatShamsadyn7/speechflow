using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using SpeechFlow.Core;
using SpeechFlow.Core.Speech;

namespace SpeechFlow.UI;

public partial class SettingsWindow : Window
{
    const string DefaultMicrophone = "Windows default";

    readonly DictationController _controller;
    readonly ObservableCollection<Snippet> _snippets;
    CancellationTokenSource? _download;

    public SettingsWindow(DictationController controller)
    {
        InitializeComponent();
        _controller = controller;
        var s = controller.Settings;

        foreach (var model in ModelStore.Models)
            ModelBox.Items.Add(new ComboBoxItem { Tag = model.Id, Content = model.DisplayName });

        GroqKeyBox.Password = s.GroqKey;
        LivePreviewBox.IsChecked = s.LivePreview;
        Select(EngineBox, s.Engine);
        Select(ModelBox, s.LocalModel);
        Select(LanguageBox, s.Language);
        Select(TypingModeBox, s.TypingMode);
        Select(PolishModeBox, s.PolishMode);

        MicrophoneBox.Items.Add(DefaultMicrophone);
        foreach (var name in AudioCapture.ListDevices()) MicrophoneBox.Items.Add(name);
        MicrophoneBox.SelectedItem = MicrophoneBox.Items.Contains(s.Microphone) ? s.Microphone : DefaultMicrophone;

        AutoStopBox.Text = s.AutoStopSeconds.ToString();
        VoiceCommandsBox.IsChecked = s.VoiceCommands;
        TashkeelBox.IsChecked = s.StripTashkeel;
        SoundsBox.IsChecked = s.Sounds;
        HistoryBox.IsChecked = s.SaveHistory;
        StartupBox.IsChecked = s.StartWithWindows;

        ToggleHotkeyBox.Text = s.ToggleHotkey;
        LanguageHotkeyBox.Text = s.LanguageHotkey;
        UndoHotkeyBox.Text = s.UndoHotkey;
        PolishHotkeyBox.Text = s.PolishHotkey;

        _snippets = new ObservableCollection<Snippet>(
            controller.Snippets.Select(x => new Snippet { Trigger = x.Trigger, Text = x.Text }));
        SnippetGrid.ItemsSource = _snippets;

        UpdateEnginePanels();
        UpdateModelStatus();
    }

    WhisperModel SelectedModel => ModelStore.Find(SelectedTag(ModelBox, "small"));

    void OnEngineChanged(object sender, SelectionChangedEventArgs e) => UpdateEnginePanels();

    void OnLivePreviewChanged(object sender, RoutedEventArgs e) => UpdateEnginePanels();

    void OnModelChanged(object sender, SelectionChangedEventArgs e) => UpdateModelStatus();

    void UpdateEnginePanels()
    {
        if (LocalPanel is null) return; // SelectionChanged fires during InitializeComponent
        bool needsModel = SelectedTag(EngineBox, "groq") == "local" || LivePreviewBox.IsChecked == true;
        LocalPanel.Visibility = needsModel ? Visibility.Visible : Visibility.Collapsed;
    }

    void UpdateModelStatus()
    {
        if (ModelStatus is null || _download is not null) return;
        bool ready = ModelStore.IsDownloaded(SelectedModel);
        ModelStatus.Text = ready ? "✓ Downloaded" : "Not downloaded yet (it downloads on first use)";
        DownloadButton.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
        DeleteModelButton.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
    }

    async void OnDownload(object sender, RoutedEventArgs e)
    {
        if (_download is not null)
        {
            _download.Cancel();
            return;
        }

        var model = SelectedModel;
        _download = new CancellationTokenSource();
        DownloadButton.Content = "Cancel";
        ModelBox.IsEnabled = false;
        DownloadProgress.Value = 0;
        DownloadProgress.Visibility = Visibility.Visible;
        var progress = new Progress<double>(f =>
        {
            DownloadProgress.Value = f;
            ModelStatus.Text = $"Downloading… {f * 100:0}%";
        });

        try
        {
            await ModelStore.DownloadAsync(model, progress, _download.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (SpeechException ex)
        {
            ErrorText.Text = ex.Message;
        }
        finally
        {
            _download.Dispose();
            _download = null;
            DownloadButton.Content = "Download";
            ModelBox.IsEnabled = true;
            DownloadProgress.Visibility = Visibility.Collapsed;
            UpdateModelStatus();
        }
    }

    void OnDeleteModel(object sender, RoutedEventArgs e)
    {
        var model = SelectedModel;
        if (_controller.IsListening && _controller.Settings.Engine == "local")
        {
            ErrorText.Text = "Stop dictation before deleting the model.";
            return;
        }
        try
        {
            File.Delete(ModelStore.PathOf(model));
        }
        catch (IOException)
        {
            ErrorText.Text = "The model is in use. Restart SpeechFlow, then delete it.";
        }
        UpdateModelStatus();
    }

    void OnSave(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        if (!int.TryParse(AutoStopBox.Text.Trim(), out int autoStop) || autoStop < 0 || autoStop > 3600)
        {
            ErrorText.Text = "Auto-stop must be a number of seconds between 0 and 3600.";
            return;
        }

        var hotkeys = new[] { ToggleHotkeyBox, LanguageHotkeyBox, UndoHotkeyBox, PolishHotkeyBox };
        var invalid = hotkeys.Select(b => b.Text.Trim())
            .Where(t => t.Length > 0 && !HotkeyGesture.TryParse(t, out _, out _))
            .ToList();
        if (invalid.Count > 0)
        {
            ErrorText.Text = "Not a valid shortcut: " + string.Join(", ", invalid);
            return;
        }
        if (string.IsNullOrWhiteSpace(ToggleHotkeyBox.Text))
        {
            ErrorText.Text = "The start / stop shortcut can't be empty.";
            return;
        }

        var s = _controller.Settings.Clone();
        s.GroqKey = GroqKeyBox.Password.Trim();
        s.Engine = SelectedTag(EngineBox, "groq");
        s.LocalModel = SelectedModel.Id;
        s.LivePreview = LivePreviewBox.IsChecked == true;
        s.Language = SelectedTag(LanguageBox, "ar");
        s.TypingMode = SelectedTag(TypingModeBox, "type");
        s.PolishMode = SelectedTag(PolishModeBox, "grammar");
        s.Microphone = MicrophoneBox.SelectedItem as string is { } mic && mic != DefaultMicrophone ? mic : "";
        s.AutoStopSeconds = autoStop;
        s.VoiceCommands = VoiceCommandsBox.IsChecked == true;
        s.StripTashkeel = TashkeelBox.IsChecked == true;
        s.Sounds = SoundsBox.IsChecked == true;
        s.SaveHistory = HistoryBox.IsChecked == true;
        s.StartWithWindows = StartupBox.IsChecked == true;
        s.ToggleHotkey = ToggleHotkeyBox.Text.Trim();
        s.LanguageHotkey = LanguageHotkeyBox.Text.Trim();
        s.UndoHotkey = UndoHotkeyBox.Text.Trim();
        s.PolishHotkey = PolishHotkeyBox.Text.Trim();

        if (s.Engine == "groq" && s.GroqKey.Length == 0)
        {
            ErrorText.Text = "The cloud engine needs a Groq API key (free). Or pick “On this PC”.";
            return;
        }

        SnippetGrid.CommitEdit(DataGridEditingUnit.Row, true);
        var failed = _controller.ApplySettings(s, _snippets.ToList());
        if (failed.Count > 0)
        {
            ErrorText.Text = "Saved, but another app already uses: " + string.Join(", ", failed) + ". Pick a different shortcut.";
            return;
        }
        Close();
    }

    void OnCancel(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        _download?.Cancel();
        base.OnClosing(e);
    }

    void OnClearHistory(object sender, RoutedEventArgs e)
    {
        HistoryStore.Clear();
        ErrorText.Text = "History cleared.";
    }

    void OnLink(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    static void Select(ComboBox box, string tag) =>
        box.SelectedItem = box.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == tag)
            ?? box.Items.OfType<ComboBoxItem>().First();

    static string SelectedTag(ComboBox box, string fallback) =>
        (box.SelectedItem as ComboBoxItem)?.Tag as string ?? fallback;
}
