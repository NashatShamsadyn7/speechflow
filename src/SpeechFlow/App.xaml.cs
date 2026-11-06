using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using SpeechFlow.Core;
using SpeechFlow.UI;

namespace SpeechFlow;

public partial class App : Application
{
    Mutex? _singleInstance;
    DictationController? _controller;
    BarWindow? _bar;
    TrayIcon? _tray;
    SettingsWindow? _settings;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, @"Local\SpeechFlow.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        AppPaths.EnsureDataDir();
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("Unhandled UI exception", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error("Unhandled exception", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };
        Log.Info($"SpeechFlow {typeof(App).Assembly.GetName().Version} starting");

        _controller = new DictationController(Dispatcher);
        _controller.SettingsRequested += OpenSettings;
        _bar = new BarWindow(_controller); // hidden until dictation starts
        _tray = new TrayIcon(_controller, OpenSettings, Quit);
        _controller.Initialize();

        if (!_controller.Settings.Onboarded)
        {
            var welcome = new WelcomeWindow(_controller);
            welcome.Finished += _tray.ShowReady;
            welcome.Show();
        }
        else
        {
            if (_controller.NeedsSetup || e.Args.Contains("--settings")) OpenSettings();
            else if (!e.Args.Contains(StartupRegistration.StartupArgument)) _tray.ShowReady();
            _controller.PrefetchLocalModel();
        }

        // Check for updates a little after startup, at most once a day.
        var updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        updateTimer.Tick += (_, _) =>
        {
            updateTimer.Stop();
            var last = _controller.Settings.LastUpdateCheck;
            if (last is null || DateTime.UtcNow - last.Value > TimeSpan.FromDays(1)) CheckForUpdates(manual: false);
        };
        updateTimer.Start();
    }

    /// <summary>Asks GitHub for a newer release; when found, offers it (directly if the user asked).</summary>
    public async void CheckForUpdates(bool manual)
    {
        UpdateInfo? update;
        try
        {
            update = await Updater.CheckAsync();
            _controller!.MarkUpdateChecked();
        }
        catch (Exception ex)
        {
            Log.Error("Update check failed", ex);
            if (manual) MessageBox.Show("Couldn't check for updates. Check your internet connection.", "SpeechFlow");
            return;
        }

        if (update is null)
        {
            if (manual) MessageBox.Show($"You have the latest version ({Updater.Current}).", "SpeechFlow");
            return;
        }
        if (manual) ShowUpdate(update);
        else _tray!.ShowMessage($"SpeechFlow {update.Version} is available", "Click here to update.", () => ShowUpdate(update));
    }

    void ShowUpdate(UpdateInfo update)
    {
        var window = new UpdateWindow(update, Quit);
        window.Show();
        window.Activate();
    }

    void OpenSettings()
    {
        if (Windows.OfType<WelcomeWindow>().FirstOrDefault() is { } welcome)
        {
            welcome.Activate();
            return;
        }
        if (_settings is { IsLoaded: true })
        {
            _settings.Activate();
            return;
        }
        _settings = new SettingsWindow(_controller!, () => CheckForUpdates(manual: true));
        _settings.Show();
        _settings.Activate();
    }

    void Quit()
    {
        _controller?.Dispose();
        _tray?.Dispose();
        _singleInstance?.ReleaseMutex();
        Shutdown();
    }
}
