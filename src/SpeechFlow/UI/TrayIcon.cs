using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SpeechFlow.Core;

namespace SpeechFlow.UI;

public sealed class TrayIcon : IDisposable
{
    readonly DictationController _controller;
    readonly NotifyIcon _icon;
    readonly Icon _idleIcon = Draw(Color.FromArgb(56, 189, 248));
    readonly Icon _listeningIcon = Draw(Color.FromArgb(239, 68, 68));
    readonly ToolStripMenuItem _toggleItem;
    readonly ToolStripMenuItem _languageItem;
    string _background = "";

    public TrayIcon(DictationController controller, Action openSettings, Action quit)
    {
        _controller = controller;

        _toggleItem = new ToolStripMenuItem("Start dictation", null, (_, _) => controller.Toggle());
        _languageItem = new ToolStripMenuItem("", null, (_, _) => controller.SwitchLanguage());
        var menu = new ContextMenuStrip();
        menu.Items.Add(_toggleItem);
        menu.Items.Add(_languageItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings", null, (_, _) => openSettings());
        menu.Items.Add("Open data folder", null, (_, _) => OpenDataFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit SpeechFlow", null, (_, _) => quit());

        _icon = new NotifyIcon { Icon = _idleIcon, Visible = true, ContextMenuStrip = menu };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) openSettings();
        };

        controller.StateChanged += Update;
        controller.Notify += message => _icon.ShowBalloonTip(5000, "SpeechFlow", message, ToolTipIcon.Info);
        controller.BackgroundStatus += text =>
        {
            _background = text;
            Update();
        };
        Update();
    }

    /// <summary>Tells the user the app is running, since the bar stays hidden until they dictate.</summary>
    public void ShowReady() => _icon.ShowBalloonTip(6000, "SpeechFlow is ready",
        $"Tap {_controller.Settings.ToggleHotkey} to dictate, or hold it while you talk.", ToolTipIcon.Info);

    public void ShowMessage(string title, string text, Action? onClick = null)
    {
        void Clicked(object? sender, EventArgs e)
        {
            _icon.BalloonTipClicked -= Clicked;
            onClick?.Invoke();
        }
        if (onClick is not null) _icon.BalloonTipClicked += Clicked;
        _icon.ShowBalloonTip(8000, title, text, ToolTipIcon.Info);
    }

    void Update()
    {
        bool listening = _controller.IsListening;
        _icon.Icon = listening ? _listeningIcon : _idleIcon;
        var tip = listening ? "SpeechFlow — listening"
            : _background.Length > 0 ? "SpeechFlow — " + _background
            : $"SpeechFlow — {_controller.Settings.ToggleHotkey} to dictate";
        _icon.Text = tip.Length > 63 ? tip[..63] : tip; // Windows limit for tray tooltips
        _toggleItem.Text = listening ? "Stop dictation" : "Start dictation";
        _languageItem.Text = $"Mode: {_controller.ModeLabel} (click to switch)";
    }

    static void OpenDataFolder()
    {
        AppPaths.EnsureDataDir();
        Process.Start(new ProcessStartInfo(AppPaths.DataDir) { UseShellExecute = true });
    }

    /// <summary>A filled circle with a simple microphone, so idle and listening states differ at a glance.</summary>
    static Icon Draw(Color color)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var fill = new SolidBrush(color);
            g.FillEllipse(fill, 1, 1, 30, 30);
            using var white = new SolidBrush(Color.White);
            using var pen = new Pen(Color.White, 2.2f);
            g.FillRectangle(white, 13, 7, 6, 11);
            g.FillEllipse(white, 13, 4, 6, 6);
            g.FillEllipse(white, 13, 15, 6, 6);
            g.DrawArc(pen, 9, 10, 14, 12, 0, 180);
            g.DrawLine(pen, 16, 22, 16, 26);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
