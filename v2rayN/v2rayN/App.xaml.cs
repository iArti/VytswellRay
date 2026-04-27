namespace v2rayN;

public partial class App : Application
{
    public static EventWaitHandle ProgramStarted = null!;

    /// <summary>
    /// True when sing-box.exe is not found. All core operations are simulated so
    /// the UI can be tested without the real binary present.
    /// </summary>
    public static bool UITestMode { get; private set; }

    public App()
    {
        DispatcherUnhandledException += (_, e) => { Logging.SaveLog("App_DispatcherUnhandledException", e.Exception); e.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) Logging.SaveLog("CurrentDomain_UnhandledException", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) => Logging.SaveLog("TaskScheduler_UnobservedTaskException", e.Exception);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        var exePathKey = Utils.GetMd5(Utils.GetExePath());
        ProgramStarted = new EventWaitHandle(false, EventResetMode.AutoReset, exePathKey, out var bCreatedNew);
        if (!bCreatedNew)
        {
            ProgramStarted.Set();
            Environment.Exit(0);
            return;
        }

        if (!AppManager.Instance.InitApp())
        {
            UI.Show("Loading config failed");
            Environment.Exit(0);
            return;
        }

        AppManager.Instance.InitComponents();

        // RU is the default. Honor any saved choice in config.UiItem.CurrentLanguage.
        var saved = AppManager.Instance.Config.UiItem.CurrentLanguage;
        LocalizedStrings.Instance.SetLanguage(saved == null || saved.StartsWith("ru", StringComparison.OrdinalIgnoreCase));

        // Detect sing-box binary — activate UI test mode when absent
        var singboxExe = Utils.GetBinPath(Utils.GetExeName("sing-box"), ECoreType.sing_box.ToString());
        UITestMode = !File.Exists(singboxExe);
        if (UITestMode) Logging.SaveLog("sing-box not found — UI test mode active");

        // Seed a demo profile on first launch so the Connect button is usable immediately
        _ = SeedDemoProfileAsync();

        // Initialize core process management
        _ = CoreManager.Instance.Init(AppManager.Instance.Config, async (_, msg) =>
        {
            await Task.Yield();
            if (!string.IsNullOrEmpty(msg)) Logging.SaveLog($"core: {msg}");
        });

        RxAppBuilder.CreateReactiveUIBuilder()
            .WithWpf()
            .BuildApp();

        var window = new Views.MainWindow();
        MainWindow = window;
        window.Show();

        base.OnStartup(e);
    }

    private static async Task SeedDemoProfileAsync()
    {
        try
        {
            var existing = await AppManager.Instance.ProfileItems(string.Empty);
            if (existing?.Count > 0) return;

            const string demoUrl =
                "vless://00000000-0000-0000-0000-000000000000@demo.example.com:443" +
                "?type=tcp&security=tls&sni=demo.example.com#Demo+Connection";

            var profile = FmtHandler.ResolveConfig(demoUrl, out _);
            if (profile == null) return;

            var config = AppManager.Instance.Config;
            await ConfigHandler.AddServer(config, profile);
            await ConfigHandler.SetDefaultServerIndex(config, profile.IndexId);
        }
        catch { /* best-effort */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logging.SaveLog("OnExit");
        try { CoreManager.Instance.CoreStop().Wait(2000); } catch { }
        base.OnExit(e);
        Process.GetCurrentProcess().Kill();
    }
}
