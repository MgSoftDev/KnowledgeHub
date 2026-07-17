using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using KnowledgeHub.Demo.SharedAuth;
using KnowledgeHub.Demo.Wpf.Auth;
using MgSoftDev.KnowledgeHub;
using MgSoftDev.KnowledgeHub.Blazor;
using MgSoftDev.KnowledgeHub.Blazor.EditorTools;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Seeding;
using MgSoftDev.KnowledgeHub.Storage.LiteDb;
using MgSoftDev.ReturningCore.Logger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace KnowledgeHub.Demo.Wpf;

/// <summary>
/// Demo HOST application (WPF + Blazor Hybrid + LiteDB). Simulates a real system that owns its
/// users/roles and embeds the KnowledgeHub module through the NuGet-style packages.
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private Mutex? _singleInstanceMutex;

    /// <summary>Root service provider, exposed so the BlazorWebView can resolve its services.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single instance guard.
        _singleInstanceMutex = new Mutex(true, @"Global\KnowledgeHubDemo.SingleInstance", out var isNew);
        if (!isNew)
        {
            MessageBox.Show("KnowledgeHub Demo ya se está ejecutando.", "KnowledgeHub Demo",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var culture = new CultureInfo("es-MX");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "Logs", "log-.txt"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
#if DEBUG
            .WriteTo.Console()
#endif
            .CreateLogger();

        // Route the Returning library's logging into Serilog.
        ReturningLogger.LoggerService = new SerilogReturningLoggerService();

        try
        {
            var dataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KnowledgeHubDemo");
            Directory.CreateDirectory(dataFolder);

            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                ContentRootPath = AppContext.BaseDirectory
            });
            builder.Services.AddSerilog();

            // ---- Demo HOST plumbing: its own users/roles in its own LiteDB file. ----------
            builder.Services.AddDemoAuth(Path.Combine(dataFolder, "demo-auth.db"));
            builder.Services.AddSingleton<DemoUserContext>();
            builder.Services.AddSingleton<IKnowledgeHubUserContext>(sp => sp.GetRequiredService<DemoUserContext>());

            // ---- KnowledgeHub module: LiteDB store + core + disk cache + Blazor UI. -------
            builder.Services.AddKnowledgeHubLiteDbStore(Path.Combine(dataFolder, "demo-knowledgehub.db"));
            builder.Services.AddKnowledgeHubCore(o => o.PublicAssetsBaseUrl = "https://docs-assets");
            builder.Services.AddKnowledgeHubFileImageCache(Path.Combine(dataFolder, "cache"));
            builder.Services.AddKnowledgeHubBlazor(o =>
            {
                o.PortalTitle = "📚 KnowledgeHub Demo";
                o.HeaderActionsComponent = typeof(SharedAuth.Components.HostLinks);

                // A HOST-provided editor tool, registered exactly like the built-in callouts.
                o.EditorTools.Add(new EditorToolDescriptor
                {
                    CommandName = "DemoFirma",
                    Icon = "draw",
                    Title = "Insertar firma del autor",
                    ExecuteAsync = ctx => Task.FromResult<string?>(
                        $"<p><em>— {ctx.User.DisplayName}, {DateTime.Now:dd/MM/yyyy HH:mm}</em></p>")
                });
            });

            // ---- WPF hosting. ---------------------------------------------------------------
            builder.Services.AddWpfBlazorWebView();
#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
#endif
            builder.Services.AddTransient<LoginWindow>();
            builder.Services.AddTransient<MainWindow>();

            _host = builder.Build();
            Services = _host.Services;
            await _host.StartAsync();

            await SeedAsync();

#if DEBUG
            if (await TryDevAutoLoginAsync())
            {
                var autoMain = _host.Services.GetRequiredService<MainWindow>();
                MainWindow = autoMain;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                autoMain.Show();
                return;
            }
#endif

            var login = _host.Services.GetRequiredService<LoginWindow>();
            if (login.ShowDialog() == true)
            {
                var main = _host.Services.GetRequiredService<MainWindow>();
                MainWindow = main;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                main.Show();
            }
            else
            {
                Shutdown();
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal error during application startup");
            MessageBox.Show($"Error al iniciar la aplicación:\n{ex.Message}", "KnowledgeHub Demo",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private async Task SeedAsync()
    {
        // Host users/roles.
        var userSeed = await _host!.Services.GetRequiredService<DemoUserSeeder>().SeedIfEmptyAsync();
        if (!userSeed.Ok)
            Log.Warning("Demo user seeding did not complete cleanly.");

        // KnowledgeHub sample content, with the restricted demo pages mapped to host roles.
        using var scope = _host.Services.CreateScope();
        var contentSeeder = scope.ServiceProvider.GetRequiredService<KnowledgeHubContentSeeder>();
        var contentSeed = await contentSeeder.SeedSampleContentIfEmptyAsync(new Dictionary<string, string[]>
        {
            ["documentacion-tecnica"] = new[] { DemoPermissions.RolePrefix + "Editor" },
            ["produccion"] = new[] { DemoPermissions.RolePrefix + "Produccion" },
            ["oficinas"] = new[] { DemoPermissions.RolePrefix + "Oficinas" }
        });
        if (!contentSeed.Ok)
            Log.Warning("KnowledgeHub content seeding did not complete cleanly.");
    }

#if DEBUG
    /// <summary>
    /// Development convenience: when KNOWLEDGEHUB_AUTOLOGIN holds a user name, sign that user
    /// in directly (no password) and skip the login window. Never compiled in Release.
    /// </summary>
    private async Task<bool> TryDevAutoLoginAsync()
    {
        var userName = Environment.GetEnvironmentVariable("KNOWLEDGEHUB_AUTOLOGIN");
        if (string.IsNullOrWhiteSpace(userName)) return false;

        var auth = _host!.Services.GetRequiredService<DemoAuthService>();
        var user = await auth.GetUserAsync(userName);
        if (!user.OkNotNull) return false;

        _host.Services.GetRequiredService<DemoUserContext>().SetUser(user.Value!);
        Log.Information("Dev auto-login as {UserName}", userName);
        return true;
    }
#endif

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(2));
            _host.Dispose();
        }
        await Log.CloseAndFlushAsync();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
