using System.Reactive;
using Microsoft.Win32;

namespace v2rayN.ViewModels;

public class SettingsDialogViewModel : ReactiveObject
{
    private readonly Config _config = AppManager.Instance.Config;

    // Easter-egg state
    private const int SecretClickThreshold = 7;
    private const int SecretWindowMs = 3000;
    private int _secretClickCount;
    private DateTime _secretFirstClickAt;

    public SettingsDialogViewModel()
    {
        ExportLogsCommand     = ReactiveCommand.Create(ExportLogs);
        OpenLogFolderCommand  = ReactiveCommand.Create(OpenLogFolder);
        UpdateGeoNowCommand   = ReactiveCommand.CreateFromTask(UpdateGeoNowAsync);
        VersionTappedCommand  = ReactiveCommand.Create(OnVersionTapped);

        // If user previously enabled the hidden setting, keep it visible.
        IsSecretRevealed = !_config.GuiItem.RouteBittorrentDirect;
    }

    #region Toggles

    public bool AutoStart
    {
        get => _config.GuiItem.AutoRun;
        set
        {
            if (_config.GuiItem.AutoRun == value) return;
            _config.GuiItem.AutoRun = value;
            this.RaisePropertyChanged();
            _ = ConfigHandler.SaveConfig(_config);
        }
    }

    public bool AutoConnect
    {
        get => _config.GuiItem.AutoConnect;
        set
        {
            if (_config.GuiItem.AutoConnect == value) return;
            _config.GuiItem.AutoConnect = value;
            this.RaisePropertyChanged();
            _ = ConfigHandler.SaveConfig(_config);
        }
    }

    public bool RouteBittorrentViaProxy
    {
        get => !_config.GuiItem.RouteBittorrentDirect;
        set
        {
            var newDirect = !value;
            if (_config.GuiItem.RouteBittorrentDirect == newDirect) return;
            _config.GuiItem.RouteBittorrentDirect = newDirect;
            this.RaisePropertyChanged();
            _ = ConfigHandler.SaveConfig(_config);
        }
    }

    private bool _isSecretRevealed;
    public bool IsSecretRevealed
    {
        get => _isSecretRevealed;
        private set => this.RaiseAndSetIfChanged(ref _isSecretRevealed, value);
    }

    private string? _secretInfoMessage;
    public string? SecretInfoMessage
    {
        get => _secretInfoMessage;
        private set => this.RaiseAndSetIfChanged(ref _secretInfoMessage, value);
    }

    public string Version => $"{LocalizedStrings.Instance.SettingsVersion}: {Utils.GetVersionInfo()}";

    public string GeoUpdatedText
    {
        get
        {
            var s = LocalizedStrings.Instance;
            var (latest, daysAgo) = GetLatestRulesetInfo();
            if (latest == null) return $"{s.SettingsGeoUpdated}: {s.SettingsGeoNever}";
            return $"{s.SettingsGeoUpdated}: {daysAgo} {(s.IsRussian ? "д. назад" : "d ago")}";
        }
    }

    private bool _isUpdatingGeo;
    public bool IsUpdatingGeo
    {
        get => _isUpdatingGeo;
        private set => this.RaiseAndSetIfChanged(ref _isUpdatingGeo, value);
    }

    #endregion

    #region Commands

    public ReactiveCommand<Unit, Unit> ExportLogsCommand   { get; }
    public ReactiveCommand<Unit, Unit> OpenLogFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> UpdateGeoNowCommand { get; }
    public ReactiveCommand<Unit, Unit> VersionTappedCommand { get; }

    private void ExportLogs()
    {
        var s = LocalizedStrings.Instance;
        var dlg = new SaveFileDialog
        {
            Title = s.LogsExportTitle,
            Filter = s.LogsExportFilter,
            FileName = $"vytswellray-logs-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
        };
        if (dlg.ShowDialog() != true) return;

        var ok = LogExporter.ExportTo(dlg.FileName);
        SecretInfoMessage = ok ? s.LogsExportDone : s.LogsExportFailed;
    }

    private void OpenLogFolder()
    {
        var path = Utils.GetLogPath();
        if (Directory.Exists(path))
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true }); }
            catch { }
        }
    }

    private async Task UpdateGeoNowAsync()
    {
        IsUpdatingGeo = true;
        try
        {
            // Stop + start of the core forces sing-box to re-evaluate rule_set update_interval.
            var wasRunning = AppManager.Instance.RunningCoreType == ECoreType.sing_box;
            await CoreManager.Instance.CoreStop();
            if (wasRunning)
            {
                var profile = await AppManager.Instance.GetProfileItem(_config.IndexId);
                if (profile != null) await CoreManager.Instance.LoadCoreFromProfile(profile);
            }
            await Task.Delay(1500);
            this.RaisePropertyChanged(nameof(GeoUpdatedText));
        }
        finally
        {
            IsUpdatingGeo = false;
        }
    }

    private void OnVersionTapped()
    {
        var now = DateTime.UtcNow;
        if (_secretClickCount == 0 || (now - _secretFirstClickAt).TotalMilliseconds > SecretWindowMs)
        {
            _secretClickCount = 1;
            _secretFirstClickAt = now;
            return;
        }
        _secretClickCount++;
        if (_secretClickCount >= SecretClickThreshold && !IsSecretRevealed)
        {
            IsSecretRevealed = true;
            SecretInfoMessage = LocalizedStrings.Instance.SecretUnlocked;
        }
    }

    #endregion

    private (DateTime? latest, int daysAgo) GetLatestRulesetInfo()
    {
        try
        {
            var dir = Path.Combine(Utils.GetBinPath(""), "srss");
            if (!Directory.Exists(dir)) return (null, 0);
            var files = Directory.GetFiles(dir, "*.srs");
            if (files.Length == 0) return (null, 0);
            var latest = files.Max(File.GetLastWriteTime);
            var days = (int)(DateTime.Now - latest).TotalDays;
            return (latest, Math.Max(0, days));
        }
        catch { return (null, 0); }
    }
}
