using Wpf.Ui.Controls;

namespace v2rayN.Views;

public partial class SettingsDialog : ContentDialog
{
    public SettingsDialogViewModel ViewModel { get; }

    public SettingsDialog(ContentDialogHost dialogHost)
        : base(dialogHost)
    {
        ViewModel = new SettingsDialogViewModel();
        InitializeComponent();
        DataContext = ViewModel;
        CloseButtonText = LocalizedStrings.Instance.Close;
    }
}
