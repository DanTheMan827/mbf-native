using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using XamlApplication = Microsoft.UI.Xaml.Application;
using ModsBeforeFriday.App.Services;
using ModsBeforeFriday.Application.Services;
using ModsBeforeFriday.Backend;
using ModsBeforeFriday.Backend.Configuration;

namespace ModsBeforeFriday.App;

public partial class App : XamlApplication
{
    private ILoggerFactory? _loggerFactory;

    public App()
    {
        InitializeComponent();
    }

    public static App CurrentApp => (App)XamlApplication.Current;

    public BackendRuntime Backend { get; private set; } = null!;

    public AppState State { get; private set; } = null!;

    public SettingsStore SettingsStore { get; private set; } = null!;

    public IUserInteractionService Interaction { get; private set; } = null!;

    public MainWindow? MainWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        SettingsStore = new SettingsStore();
        var settings = SettingsStore.Load();
        State = new AppState(settings);

        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddDebug();
        });

        var agentPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Agent", "mbf-agent");
        var bundledAdb = Path.Combine(AppContext.BaseDirectory, "Assets", "PlatformTools", "adb.exe");
        var isLocalAdb = settings.AdbHost.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || settings.AdbHost == "127.0.0.1"
            || settings.AdbHost == "::1";

        var options = new BackendOptions
        {
            AgentBinaryPath = agentPath,
            AdbExecutablePath = settings.AdbExecutablePath ?? (File.Exists(bundledAdb) ? bundledAdb : null),
            GameId = settings.GameId,
            IgnorePackageId = settings.IgnorePackageId,
            StartLocalAdbServerIfUnavailable = isLocalAdb,
        };

        Backend = BackendRuntime.CreateDefault(options, _loggerFactory, settings.AdbHost, settings.AdbPort);

        MainWindow = new MainWindow();
        Interaction = new WindowsUserInteractionService(MainWindow);
        MainWindow.Closed += (_, _) =>
        {
            Backend.Dispose();
            _loggerFactory?.Dispose();
        };

        if (MainWindow.Content is FrameworkElement root)
        {
            root.RequestedTheme = ElementTheme.Dark;
        }

        MainWindow.Activate();
        MainWindow.NavigateHome();
    }
}
