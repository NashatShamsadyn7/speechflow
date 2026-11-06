using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using SpeechFlow.Core;

namespace SpeechFlow.UI;

/// <summary>
/// The floating dictation bar. It never takes focus (WS_EX_NOACTIVATE), so clicking the mic
/// leaves the cursor in the app you are typing into.
/// </summary>
public partial class BarWindow : Window
{
    const int PreviewChars = 70;

    readonly DictationController _controller;
    readonly Rectangle[] _bars;
    readonly DispatcherTimer _meterTimer;
    double _shownLevel;
    double _phase;

    public BarWindow(DictationController controller)
    {
        InitializeComponent();
        _controller = controller;
        _bars = Meter.Children.OfType<Rectangle>().ToArray();
        _meterTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, (_, _) => AnimateMeter(), Dispatcher);

        controller.StateChanged += UpdateState;
        controller.StatusChanged += ShowStatus;
        controller.PreviewChanged += ShowPreview;

        UpdateState();
        PlaceOnScreen();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        style |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
    }

    void PlaceOnScreen()
    {
        var area = SystemParameters.WorkArea;
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var s = _controller.Settings;
        bool savedIsVisible = s.BarLeft is double left && s.BarTop is double top
            && left >= virtualLeft && top >= virtualTop
            && left + Width <= virtualLeft + SystemParameters.VirtualScreenWidth
            && top + Height <= virtualTop + SystemParameters.VirtualScreenHeight;

        if (savedIsVisible)
        {
            Left = s.BarLeft!.Value;
            Top = s.BarTop!.Value;
        }
        else
        {
            Left = area.Left + (area.Width - Width) / 2;
            Top = area.Bottom - Height - 24;
        }
    }

    void UpdateState()
    {
        bool listening = _controller.IsListening;
        bool arabic = _controller.Settings.Language == "ar";

        MicButton.Content = listening ? "" : "";
        MicButton.Background = (Brush)FindResource(listening ? "Recording" : "Accent");
        MicButton.Foreground = listening ? Brushes.White : (Brush)new BrushConverter().ConvertFrom("#0B1220")!;
        MicButton.ToolTip = $"{(listening ? "Stop" : "Start")} dictation ({_controller.Settings.ToggleHotkey})";

        LanguageButton.Content = arabic ? "AR" : "EN";

        if (listening) _meterTimer.Start();
        else
        {
            _meterTimer.Stop();
            _shownLevel = 0;
            foreach (var bar in _bars) bar.Height = 3;
        }
    }

    void ShowStatus(string text, StatusKind kind)
    {
        StatusText.Text = text;
        StatusDot.Fill = kind switch
        {
            StatusKind.Ok => (Brush)new BrushConverter().ConvertFrom("#22C55E")!,
            StatusKind.Busy => (Brush)new BrushConverter().ConvertFrom("#F59E0B")!,
            StatusKind.Error => (Brush)FindResource("Recording"),
            _ => (Brush)FindResource("TextMuted"),
        };
        if (kind == StatusKind.Error && !IsVisible) Show();
    }

    void ShowPreview(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            PreviewText.Text = "SpeechFlow";
            PreviewText.FlowDirection = FlowDirection.LeftToRight;
            PreviewText.Foreground = (Brush)FindResource("TextMuted");
            return;
        }
        PreviewText.FlowDirection = _controller.Settings.Language == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        PreviewText.Foreground = (Brush)FindResource("TextPrimary");
        PreviewText.Text = text.Length > PreviewChars ? "…" + text[^PreviewChars..] : text;
    }

    void AnimateMeter()
    {
        // Speech RMS is usually 0.02–0.2; scale it into the 0..1 range and smooth it.
        double target = Math.Min(1.0, _controller.Level * 7);
        _shownLevel += (target - _shownLevel) * (target > _shownLevel ? 0.6 : 0.2);
        _phase += 0.35;
        for (int i = 0; i < _bars.Length; i++)
        {
            double wave = 0.6 + 0.4 * Math.Sin(_phase + i * 1.1);
            _bars[i].Height = 3 + 21 * _shownLevel * wave;
        }
    }

    void OnDragStart(object sender, MouseButtonEventArgs e)
    {
        try { DragMove(); }
        catch (InvalidOperationException) { return; }
        _controller.SaveBarPosition(Left, Top);
    }

    void OnMicClick(object sender, RoutedEventArgs e) => _controller.Toggle();
    void OnLanguageClick(object sender, RoutedEventArgs e) => _controller.SwitchLanguage();
    void OnSettingsClick(object sender, RoutedEventArgs e) => _controller.RequestSettings();
    void OnHideClick(object sender, RoutedEventArgs e) => Hide();
}
