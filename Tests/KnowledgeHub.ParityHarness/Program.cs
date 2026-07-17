using KnowledgeHub.ParityHarness;
using MgSoftDev.KnowledgeHub;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Http.Client;
using MgSoftDev.KnowledgeHub.Http.Server;
using MgSoftDev.KnowledgeHub.Storage.LiteDb;
using MgSoftDev.KnowledgeHub.Storage.SqlServer;
using MgSoftDev.ReturningCore.Logger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

ReturningLogger.LoggerService = new ConsoleReturningLoggerService();

var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "inmemory";
Console.WriteLine($"== KnowledgeHub Parity Harness — store: {mode} ==");
Console.WriteLine();

var user = new HarnessUserContext();

// Fresh cache folder / database file per run so expectations are deterministic.
var cacheFolder = Path.Combine(Path.GetTempPath(), "kh-parity-cache-" + Guid.NewGuid().ToString("N"));
var liteDbPath = Path.Combine(Path.GetTempPath(), "kh-parity-" + Guid.NewGuid().ToString("N") + ".db");

int failures;

if (mode == "http")
{
    // ---- The FULL parity script through Http.Client against a REAL Kestrel server. ----------
    const string url = "http://127.0.0.1:5599";

    var serverBuilder = WebApplication.CreateBuilder();
    serverBuilder.Logging.ClearProviders();
    serverBuilder.WebHost.UseUrls(url);
    serverBuilder.Services.AddHttpContextAccessor();
    serverBuilder.Services.AddScoped<IKnowledgeHubUserContext, HeaderUserContext>();
    serverBuilder.Services.AddKnowledgeHubLiteDbStore(liteDbPath);
    serverBuilder.Services.AddKnowledgeHubCore(o => o.PublicAssetsBaseUrl = "/kh/assets");
    serverBuilder.Services.AddKnowledgeHubFileImageCache(cacheFolder);

    var server = serverBuilder.Build();
    server.MapKnowledgeHubApi();
    await server.StartAsync();
    Console.WriteLine($"   Kestrel: {url} · LiteDB: {liteDbPath}");

    // ---- Client provider: only Abstractions + Http.Client (like a WASM app). ----------------
    var clientServices = new ServiceCollection();
    clientServices.AddSingleton<IKnowledgeHubUserContext>(user);
    clientServices.AddSingleton(new HttpClient(new HarnessAuthHandler(user)) { BaseAddress = new Uri(url) });
    clientServices.AddKnowledgeHubHttpClient();

    var clientProvider = clientServices.BuildServiceProvider();
    try
    {
        await using var clientScope = clientProvider.CreateAsyncScope();
        await using var serverScope = server.Services.CreateAsyncScope();

        // Smoke check of /me before the script (identity bootstrap for remote clients).
        user.SetUser("admin", "Administrador", MgSoftDev.KnowledgeHub.Security.KnowledgeHubPermissions.Admin);
        var http = clientScope.ServiceProvider.GetRequiredService<HttpClient>();
        var me = await System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsync<
            MgSoftDev.KnowledgeHub.Transport.MeResponse>(http, "/kh/api/me");
        Console.WriteLine(me is { IsAuthenticated: true, UserName: "admin" } && me.Catalog.Count == 3
            ? "  [PASS] GET /kh/api/me devuelve identidad y catálogo"
            : "  [FAIL] GET /kh/api/me devuelve identidad y catálogo");

        failures = await ParityScript.RunAsync(clientScope.ServiceProvider, user,
            seederProvider: serverScope.ServiceProvider);
    }
    finally
    {
        await clientProvider.DisposeAsync();
        await server.StopAsync();
        await server.DisposeAsync();
    }
}
else
{
    var services = new ServiceCollection();
    services.AddSingleton<IKnowledgeHubUserContext>(user);

    switch (mode)
    {
        case "inmemory":
            services.AddSingleton<IKnowledgeHubStore>(new InMemoryKnowledgeHubStore());
            break;

        case "litedb":
            services.AddKnowledgeHubLiteDbStore(liteDbPath);
            Console.WriteLine($"   LiteDB: {liteDbPath}");
            break;

        case "sqlserver":
        {
            // Connection string via arg[1] or KH_SQLSERVER_CS. The database must exist and be
            // EMPTY of KnowledgeHub data (the script asserts exact seed/version numbers).
            var connectionString = args.Length > 1 ? args[1] : Environment.GetEnvironmentVariable("KH_SQLSERVER_CS");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine("Falta la cadena de conexión (arg 2 o env KH_SQLSERVER_CS).");
                return 2;
            }

            var ensure = await KnowledgeHubSqlSchema.EnsureDatabaseObjectsAsync(connectionString);
            if (!ensure.Ok)
            {
                Console.WriteLine($"No se pudieron crear las tablas: {ensure.ErrorInfo?.ErrorMessage}");
                return 3;
            }
            Console.WriteLine("   Tablas verificadas/creadas con el script DDL embebido (schema 'kh').");

            services.AddKnowledgeHubSqlServerStore(connectionString);
            break;
        }

        default:
            Console.WriteLine($"Store desconocido: '{mode}'. Modos disponibles: inmemory | litedb | sqlserver | http");
            return 2;
    }

    services.AddKnowledgeHubCore(o => o.PublicAssetsBaseUrl = "/kh/assets");
    services.AddKnowledgeHubFileImageCache(cacheFolder);

    var provider = services.BuildServiceProvider();
    try
    {
        await using var scope = provider.CreateAsyncScope();
        failures = await ParityScript.RunAsync(scope.ServiceProvider, user);
    }
    finally
    {
        // Release the LiteDatabase file lock before cleaning up the temp files.
        await provider.DisposeAsync();
    }
}

try { Directory.Delete(cacheFolder, recursive: true); } catch { /* best effort */ }
try { if (File.Exists(liteDbPath)) File.Delete(liteDbPath); } catch { /* best effort */ }

return failures == 0 ? 0 : 1;
