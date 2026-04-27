using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Tray.Controls;

namespace v2rayN.Views;

public partial class MainWindow : FluentWindow
{
    private readonly MainWindowViewModel _vm;

    public MainWindow()
    {
        _vm = new MainWindowViewModel();
        InitializeComponent();
        DataContext = _vm;
        SystemThemeWatcher.Watch(this);

        _vm.ShowConnectionsRequested += async () =>
        {
            var dialog = new ConnectionsDialog(DialogHost, this);
            await dialog.ShowAsync();
        };

        _vm.ShowSettingsRequested += async () =>
        {
            var dialog = new SettingsDialog(DialogHost);
            await dialog.ShowAsync();
        };


        // Resurrect window when a 2nd instance signals via the EventWaitHandle
        ThreadPool.RegisterWaitForSingleObject(App.ProgramStarted, OnProgramStarted, null, -1, false);

        // Auto-connect on launch
        Loaded += OnLoaded;

        // Hide-to-tray instead of close
        Closing += (s, e) =>
        {
            if (_vm.State == ConnectionState.Disconnected) return; // allow real close when not running
            e.Cancel = true;
            Hide();
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (AppManager.Instance.Config.GuiItem.AutoConnect)
        {
            // Brief pause so the UI settles before triggering connect
            await Task.Delay(800);
            await _vm.ConnectAsync();
        }
    }

    private void OnProgramStarted(object? state, bool timedOut)
    {
        Dispatcher.Invoke(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });
    }

    private void OnTrayLeftClick(NotifyIcon sender, RoutedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (Visibility != Visibility.Visible || WindowState == WindowState.Minimized)
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            }
            else
            {
                Hide();
            }
        });
    }
}
