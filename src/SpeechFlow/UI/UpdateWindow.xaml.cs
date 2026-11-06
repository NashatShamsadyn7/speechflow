using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using SpeechFlow.Core;

namespace SpeechFlow.UI;

public partial class UpdateWindow : Window
{
    readonly UpdateInfo _update;
    readonly Action _quit;
    CancellationTokenSource? _download;

    /// <param name="quit">Closes SpeechFlow so the installer can replace it.</param>
    public UpdateWindow(UpdateInfo update, Action quit)
    {
        InitializeComponent();
        _update = update;
        _quit = quit;
        TitleText.Text = $"SpeechFlow {update.Version} is available";
        BodyText.Text = $"You have {Updater.Current}. The update is {update.Bytes / 1048576.0:0.#} MB; " +
                        "SpeechFlow restarts by itself when it's done, and your settings are kept.";
    }

    async void OnUpdate(object sender, RoutedEventArgs e)
    {
        _download = new CancellationTokenSource();
        UpdateButton.IsEnabled = false;
        Progress.Visibility = Visibility.Visible;
        try
        {
            var path = await Updater.DownloadAsync(_update, new Progress<double>(f =>
            {
                Progress.Value = f;
                BodyText.Text = $"Downloading… {f * 100:0}%";
            }), _download.Token);
            BodyText.Text = "Installing…";
            Updater.Install(path);
            _quit();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Error("Update failed", ex);
            BodyText.Text = "The download failed. Check your internet connection and try again.";
            UpdateButton.IsEnabled = true;
            Progress.Visibility = Visibility.Collapsed;
        }
        finally
        {
            _download?.Dispose();
            _download = null;
        }
    }

    void OnLater(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        _download?.Cancel();
        base.OnClosing(e);
    }
}
