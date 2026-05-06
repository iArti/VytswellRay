using System.Windows.Media.Effects;
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

        // Settings: custom in-window overlay panel (not a floating ContentDialog)
        _vm.ShowSettingsRequested += async () =>
        {
            OpenSettingsOverlay();
            await Task.CompletedTask;
        };

        SettingsPanel.CloseRequested += CloseSettingsOverlay;

        // Connections: still uses WPF-UI ContentDialog + blur
        _vm.ShowConnectionsRequested += async () =>
        {
            var dialog = new ConnectionsDialog(DialogHost, this);
            await ShowWithBlur(dialog);
        };

        // Clicking the dark backdrop behind the settings card closes it
        SettingsOverlay.MouseDown += (_, e) =>
        {
            if (e.Source == SettingsOverlay || e.Source is Border { Name: "" })
                CloseSettingsOverlay();
        };

        ThreadPool.RegisterWaitForSingleObject(App.ProgramStarted, OnProgramStarted, null, -1, false);

        Loaded += OnLoaded;

        Closing += (s, e) =>
        {
            if (_vm.State == ConnectionState.Disconnected) return;
            e.Cancel = true;
            Hide();
        };
    }

    private void OpenSettingsOverlay()
    {
        MainContent.Effect = new BlurEffect { Radius = 8, KernelType = KernelType.Gaussian };
        SettingsOverlay.Visibility = Visibility.Visible;
    }

    private void CloseSettingsOverlay()
    {
        SettingsOverlay.Visibility = Visibility.Collapsed;
        MainContent.Effect = null;
    }

    // Blurs the main content while a ContentDialog (Connections) is shown.
    private async Task ShowWithBlur(ContentDialog dialog)
    {
        MainContent.Effect = new BlurEffect { Radius = 8, KernelType = KernelType.Gaussian };
        try
        {
            await dialog.ShowAsync();
        }
        finally
        {
            MainContent.Effect = null;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (AppManager.Instance.Config.GuiItem.AutoConnect)
        {
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
