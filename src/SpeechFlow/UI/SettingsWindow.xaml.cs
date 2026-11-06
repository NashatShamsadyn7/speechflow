using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using SpeechFlow.Core;

namespace SpeechFlow.UI;

public partial class SettingsWindow : Window
{
    const string DefaultMicrophone = "Windows default";

    readonly DictationController _controller;
    readonly ObservableCollection<Snippet> _snippets;

    public SettingsWindow(DictationController controller)
    {
        InitializeComponent();
        _controller = controller;
        var s = controller.Settings;

        DeepgramKeyBox.Password = s.DeepgramKey;
        GroqKeyBox.Password = s.GroqKey;
        Select(LanguageBox, s.Language);
        Select(DialectBox, s.ArabicDialect);
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
        ModelBox.Text = s.Model;

        ToggleHotkeyBox.Text = s.ToggleHotkey;
        LanguageHotkeyBox.Text = s.LanguageHotkey;
        UndoHotkeyBox.Text = s.UndoHotkey;
        PolishHotkeyBox.Text = s.PolishHotkey;

        _snippets = new ObservableCollection<Snippet>(
            controller.Snippets.Select(x => new Snippet { Trigger = x.Trigger, Text = x.Text }));
        SnippetGrid.ItemsSource = _snippets;
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
        s.DeepgramKey = DeepgramKeyBox.Password.Trim();
        s.GroqKey = GroqKeyBox.Password.Trim();
        s.Language = SelectedTag(LanguageBox, "ar");
        s.ArabicDialect = SelectedTag(DialectBox, "ar");
        s.TypingMode = SelectedTag(TypingModeBox, "type");
        s.PolishMode = SelectedTag(PolishModeBox, "grammar");
        s.Microphone = MicrophoneBox.SelectedItem as string is { } mic && mic != DefaultMicrophone ? mic : "";
        s.AutoStopSeconds = autoStop;
        s.VoiceCommands = VoiceCommandsBox.IsChecked == true;
        s.StripTashkeel = TashkeelBox.IsChecked == true;
        s.Sounds = SoundsBox.IsChecked == true;
        s.SaveHistory = HistoryBox.IsChecked == true;
        s.StartWithWindows = StartupBox.IsChecked == true;
        s.Model = string.IsNullOrWhiteSpace(ModelBox.Text) ? "nova-3" : ModelBox.Text.Trim();
        s.ToggleHotkey = ToggleHotkeyBox.Text.Trim();
        s.LanguageHotkey = LanguageHotkeyBox.Text.Trim();
        s.UndoHotkey = UndoHotkeyBox.Text.Trim();
        s.PolishHotkey = PolishHotkeyBox.Text.Trim();

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
