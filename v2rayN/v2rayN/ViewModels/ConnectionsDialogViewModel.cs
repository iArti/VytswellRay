using System.Collections.ObjectModel;
using System.Reactive;

namespace v2rayN.ViewModels;

public class ConnectionRow : ReactiveObject
{
    public string IndexId { get; init; } = "";
    public string Remarks { get; init; } = "";
    public string Address { get; init; } = "";
    public int Port { get; init; }
    public string Protocol { get; init; } = "";

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set => this.RaiseAndSetIfChanged(ref _isActive, value);
    }
}

public class ConnectionsDialogViewModel : ReactiveObject
{
    private readonly Config _config = AppManager.Instance.Config;

    public ConnectionsDialogViewModel()
    {
        PasteFromClipboardCommand = ReactiveCommand.CreateFromTask(PasteFromClipboardAsync);
        ScanQrFromScreenCommand   = ReactiveCommand.CreateFromTask<Window>(ScanQrAsync);
        DeleteCommand             = ReactiveCommand.CreateFromTask<ConnectionRow>(DeleteAsync);
        SetActiveCommand          = ReactiveCommand.CreateFromTask<ConnectionRow>(SetActiveAsync);
        _ = ReloadAsync();
    }

    #region State

    public ObservableCollection<ConnectionRow> Rows { get; } = new();

    private string? _infoMessage;
    public string? InfoMessage
    {
        get => _infoMessage;
        set => this.RaiseAndSetIfChanged(ref _infoMessage, value);
    }

    private bool _isError;
    public bool IsError
    {
        get => _isError;
        set => this.RaiseAndSetIfChanged(ref _isError, value);
    }

    private bool _isEmpty;
    public bool IsEmpty
    {
        get => _isEmpty;
        set => this.RaiseAndSetIfChanged(ref _isEmpty, value);
    }

    #endregion

    public ReactiveCommand<Unit, Unit> PasteFromClipboardCommand { get; }
    public ReactiveCommand<Window, Unit> ScanQrFromScreenCommand { get; }
    public ReactiveCommand<ConnectionRow, Unit> DeleteCommand { get; }
    public ReactiveCommand<ConnectionRow, Unit> SetActiveCommand { get; }

    private async Task PasteFromClipboardAsync()
    {
        var s = LocalizedStrings.Instance;
        var clip = WindowsUtils.GetClipboardData();
        if (string.IsNullOrWhiteSpace(clip))
        {
            ShowInfo(s.ClipboardEmpty, error: true);
            return;
        }
        await ImportFromTextAsync(clip, parseFailMessage: s.ClipboardInvalid);
    }

    private async Task ScanQrAsync(Window owner)
    {
        var s = LocalizedStrings.Instance;
        owner.WindowState = WindowState.Minimized;
        await Task.Delay(500); // let the window animate out
        byte[]? bytes;
        try
        {
            bytes = QRCodeWindowsUtils.CaptureScreen(owner);
        }
        finally
        {
            owner.WindowState = WindowState.Normal;
        }

        if (bytes == null)
        {
            ShowInfo(s.QrNotFound, error: true);
            return;
        }

        var text = QRCodeUtils.ParseBarcode(bytes);
        if (string.IsNullOrWhiteSpace(text))
        {
            ShowInfo(s.QrNotFound, error: true);
            return;
        }
        await ImportFromTextAsync(text, parseFailMessage: s.QrInvalid);
    }

    private async Task ImportFromTextAsync(string text, string parseFailMessage)
    {
        var profile = FmtHandler.ResolveConfig(text.Trim(), out var msg);
        if (profile == null)
        {
            ShowInfo(parseFailMessage, error: true);
            return;
        }

        var ret = await ConfigHandler.AddServer(_config, profile);
        if (ret != 0)
        {
            ShowInfo(parseFailMessage, error: true);
            return;
        }

        // First profile becomes active automatically
        if (Rows.Count == 0)
            await ConfigHandler.SetDefaultServerIndex(_config, profile.IndexId);

        await ReloadAsync();
        ShowInfo(LocalizedStrings.Instance.ImportSuccess, error: false);
    }

    private async Task DeleteAsync(ConnectionRow row)
    {
        var item = await AppManager.Instance.GetProfileItem(row.IndexId);
        if (item != null)
        {
            await ConfigHandler.RemoveServers(_config, [item]);
            await ReloadAsync();
        }
    }

    private async Task SetActiveAsync(ConnectionRow row)
    {
        await ConfigHandler.SetDefaultServerIndex(_config, row.IndexId);
        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        var items = await AppManager.Instance.ProfileItems(string.Empty);
        var activeId = _config.IndexId;
        Rows.Clear();
        foreach (var p in items ?? [])
        {
            Rows.Add(new ConnectionRow
            {
                IndexId  = p.IndexId,
                Remarks  = p.Remarks ?? "",
                Address  = p.Address ?? "",
                Port     = p.Port,
                Protocol = p.ConfigType.ToString(),
                IsActive = p.IndexId == activeId,
            });
        }
        IsEmpty = Rows.Count == 0;
    }

    private void ShowInfo(string message, bool error)
    {
        InfoMessage = message;
        IsError = error;
    }
}
