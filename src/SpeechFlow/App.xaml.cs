using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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

        if (!_controller.Settings.Onboarded) new WelcomeWindow(_controller).Show();
        else if (_controller.NeedsSetup) OpenSettings();
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
        _settings = new SettingsWindow(_controller!);
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
