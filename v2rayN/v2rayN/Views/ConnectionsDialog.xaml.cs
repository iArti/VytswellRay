using Wpf.Ui.Controls;

namespace v2rayN.Views;

public partial class ConnectionsDialog : ContentDialog
{
    public ConnectionsDialogViewModel ViewModel { get; }
    private readonly Window _ownerWindow;

    public ConnectionsDialog(ContentDialogHost dialogHost, Window ownerWindow)
        : base(dialogHost)
    {
        ViewModel = new ConnectionsDialogViewModel();
        _ownerWindow = ownerWindow;
        InitializeComponent();
        DataContext = ViewModel;
        ButtonText = LocalizedStrings.Instance.Close;
    }

    private string _buttonText = "";
    public string ButtonText
    {
        get => _buttonText;
        set
        {
            _buttonText = value;
            CloseButtonText = value;
        }
    }

    private void OnScanQrClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ScanQrFromScreenCommand.Execute(_ownerWindow).Subscribe();
    }
}
