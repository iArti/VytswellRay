using System.Reactive;

namespace v2rayN.ViewModels;

public class MainWindowViewModel : ReactiveObject, IDisposable
{
    private readonly Config _config = AppManager.Instance.Config;

    public MainWindowViewModel()
    {
        ToggleConnectCommand    = ReactiveCommand.CreateFromTask(ToggleConnectAsync);
        ShowConnectionsCommand  = ReactiveCommand.CreateFromTask(ShowConnectionsAsync);
        ShowSettingsCommand     = ReactiveCommand.CreateFromTask(ShowSettingsAsync);
        QuitCommand             = ReactiveCommand.CreateFromTask(QuitAsync);
        ToggleLanguageCommand   = ReactiveCommand.Create(() => LocalizedStrings.Instance.ToggleLanguage());

        // Recompute the status string whenever any of its inputs change.
        this.WhenAnyValue(x => x.State, x => x.PingDisplay, x => x.ActiveProfileName, x => x.LastError)
            .Subscribe(_ => { this.RaisePropertyChanged(nameof(StatusText)); this.RaisePropertyChanged(nameof(ActiveText)); });

        LocalizedStrings.Instance.PropertyChanged += (_, _) =>
        {
            this.RaisePropertyChanged(nameof(StatusText));
            this.RaisePropertyChanged(nameof(ActiveText));
            this.RaisePropertyChanged(nameof(ConnectButtonText));
        };

        _ = LoadActiveProfileAsync();
    }

    #region Reactive properties

    private ConnectionState _state = ConnectionState.Disconnected;
    public ConnectionState State
    {
        get => _state;
        private set => this.RaiseAndSetIfChanged(ref _state, value);
    }

    private string? _activeProfileName;
    public string? ActiveProfileName
    {
        get => _activeProfileName;
        private set => this.RaiseAndSetIfChanged(ref _activeProfileName, value);
    }

    private string? _pingDisplay;
    public string? PingDisplay
    {
        get => _pingDisplay;
        private set => this.RaiseAndSetIfChanged(ref _pingDisplay, value);
    }

    private string? _lastError;
    public string? LastError
    {
        get => _lastError;
        private set => this.RaiseAndSetIfChanged(ref _lastError, value);
    }

    public string ConnectButtonText =>
        State == ConnectionState.Connected ? LocalizedStrings.Instance.Disconnect : LocalizedStrings.Instance.Connect;

    public string StatusText
    {
        get
        {
            var s = LocalizedStrings.Instance;
            return State switch
            {
                ConnectionState.Disconnected   => $"{s.StatusPrefix}: {s.StatusDisconnected}",
                ConnectionState.Connecting     => $"{s.StatusPrefix}: {s.StatusConnecting}",
                ConnectionState.Disconnecting  => $"{s.StatusPrefix}: {s.StatusDisconnecting}",
                ConnectionState.Connected when PingDisplay is null
                    => $"{s.StatusPrefix}: {s.StatusConnected}",
                ConnectionState.Connected
                    => $"{s.StatusPrefix}: {s.StatusConnected}, {s.Ping}: {PingDisplay}",
                ConnectionState.Error          => $"{s.StatusPrefix}: {s.StatusError} — {LastError}",
                _ => "?"
            };
        }
    }

    public string ActiveText
    {
        get
        {
            var s = LocalizedStrings.Instance;
            return $"{s.ActivePrefix}: {ActiveProfileName ?? s.NoActiveProfile}";
        }
    }

    #endregion

    #region Commands

    public ReactiveCommand<Unit, Unit> ToggleConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowConnectionsCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> QuitCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleLanguageCommand { get; }

    public event Func<Task>? ShowConnectionsRequested;
    public event Func<Task>? ShowSettingsRequested;

    private async Task ShowConnectionsAsync()
    {
        if (ShowConnectionsRequested is { } h) await h();
        await LoadActiveProfileAsync();
    }

    private async Task ShowSettingsAsync()
    {
        if (ShowSettingsRequested is { } h) await h();
    }

    private async Task QuitAsync()
    {
        StopPingLoop();
        await CoreManager.Instance.CoreStop();
        Application.Current.Shutdown();
    }

    #endregion

    #region Connect / disconnect

    private async Task ToggleConnectAsync()
    {
        if (State is ConnectionState.Connecting or ConnectionState.Disconnecting) return;

        if (State == ConnectionState.Connected)
        {
            await DisconnectAsync();
        }
        else
        {
            await ConnectAsync();
        }
    }

    public async Task ConnectAsync()
    {
        var profile = await GetActiveProfileAsync();
        if (profile == null) return;

        State = ConnectionState.Connecting;
        LastError = null;

        if (App.UITestMode)
        {
            await Task.Delay(1500);
            State = ConnectionState.Connected;
            StartPingLoop(profile);
            return;
        }

        try
        {
            TunCleaner.CleanupOrphaned();
            await CoreManager.Instance.LoadCoreFromProfile(profile);
            State = ConnectionState.Connected;
            StartPingLoop(profile);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            State = ConnectionState.Error;
            Logging.SaveLog("Connect", ex);
        }
    }

    public async Task DisconnectAsync()
    {
        State = ConnectionState.Disconnecting;
        StopPingLoop();

        if (App.UITestMode)
        {
            await Task.Delay(500);
            State = ConnectionState.Disconnected;
            return;
        }

        try
        {
            await CoreManager.Instance.CoreStop();
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Disconnect", ex);
        }
        State = ConnectionState.Disconnected;
    }

    #endregion

    #region Ping loop

    private CancellationTokenSource? _pingCts;

    private void StartPingLoop(ProfileItem profile)
    {
        StopPingLoop();
        _pingCts = new CancellationTokenSource();
        _ = PingLoopAsync(profile, _pingCts.Token);
    }

    private void StopPingLoop()
    {
        _pingCts?.Cancel();
        _pingCts?.Dispose();
        _pingCts = null;
        PingDisplay = null;
    }

    private static readonly Random _rng = new();

    private async Task PingLoopAsync(ProfileItem profile, CancellationToken ct)
    {
        try
        {
            await Task.Delay(3000, ct); // let sing-box settle
            while (!ct.IsCancellationRequested)
            {
                int? ms;
                if (App.UITestMode)
                {
                    ms = _rng.Next(28, 180); // realistic-looking fake latency
                }
                else
                {
                    ms = await PingProbe.MeasureAsync(profile.Address, profile.Port, ct);
                }

                PingDisplay = ms.HasValue
                    ? $"{ms.Value} мс"
                    : LocalizedStrings.Instance.PingTimeout;
                await Task.Delay(TimeSpan.FromMinutes(5), ct);
            }
        }
        catch (OperationCanceledException) { /* normal */ }
        catch (Exception ex)
        {
            Logging.SaveLog("PingLoop", ex);
        }
    }

    #endregion

    #region Active profile

    public async Task LoadActiveProfileAsync()
    {
        var p = await GetActiveProfileAsync();
        ActiveProfileName = p?.Remarks;
    }

    private async Task<ProfileItem?> GetActiveProfileAsync()
    {
        var indexId = _config.IndexId;
        if (indexId.IsNullOrEmpty()) return null;
        return await AppManager.Instance.GetProfileItem(indexId);
    }

    #endregion

    public void Dispose()
    {
        StopPingLoop();
    }
}
