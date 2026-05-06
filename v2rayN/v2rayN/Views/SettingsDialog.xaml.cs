namespace v2rayN.Views;

public partial class SettingsDialog : UserControl
{
    public SettingsDialogViewModel ViewModel { get; }

    public event Action? CloseRequested;

    public SettingsDialog()
    {
        ViewModel = new SettingsDialogViewModel();
        InitializeComponent();
        DataContext = ViewModel;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
        => CloseRequested?.Invoke();
}
