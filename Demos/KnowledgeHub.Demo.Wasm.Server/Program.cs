using KnowledgeHub.Demo.SharedAuth;
using KnowledgeHub.Demo.Wasm.Server.Auth;
using MgSoftDev.KnowledgeHub;
using MgSoftDev.KnowledgeHub.AspNetCore;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Http.Server;
using MgSoftDev.KnowledgeHub.Seeding;
using MgSoftDev.KnowledgeHub.Storage.LiteDb;
using MgSoftDev.ReturningCore.Logger;
using Serilog;

// Demo API server hosting the Blazor WebAssembly client. Runs the KnowledgeHub core + LiteDB
// and exposes the module through MapKnowledgeHubApi/MapKnowledgeHubAssets. Same origin as the
// WASM client (hosted), so no CORS is involved.

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "Logs", "log-.txt"),
        rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
    .WriteTo.Console()
    .CreateLogger();

ReturningLogger.LoggerService = new SerilogReturningLoggerService();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSerilog();

var dataFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KnowledgeHubDemoWasm");
Directory.CreateDirectory(dataFolder);

// ---- Demo HOST plumbing: its own users/roles + opaque bearer tokens. -----------------------
builder.Services.AddDemoAuth(Path.Combine(dataFolder, "demo-auth.db"));
builder.Services.AddSingleton<TokenStore>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IKnowledgeHubUserContext, RequestUserContext>();

// ---- KnowledgeHub module: LiteDB store + core + disk cache. --------------------------------
builder.Services.AddKnowledgeHubLiteDbStore(Path.Combine(dataFolder, "demo-knowledgehub.db"));
builder.Services.AddKnowledgeHubCore(o => o.PublicAssetsBaseUrl = "/kh/assets");
builder.Services.AddKnowledgeHubFileImageCache(Path.Combine(dataFolder, "cache"));

var app = builder.Build();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// ---- Session endpoints (opaque token; the WASM client stores it in memory). -----------------
app.MapPost("/auth/login", async (LoginRequest request, DemoAuthService auth, TokenStore tokens) =>
{
    var result = await auth.LoginAsync(request.UserName, request.Password);
    if (!result.OkNotNull)
        return Results.Json(new LoginResponse(null, result.UnfinishedInfo?.Title ?? "Credenciales inválidas"),
            statusCode: StatusCodes.Status401Unauthorized);

    return Results.Ok(new LoginResponse(tokens.Issue(result.Value!), null));
});

app.MapPost("/auth/logout", (HttpContext http, TokenStore tokens) =>
{
    var header = http.Request.Headers.Authorization.ToString();
    if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        tokens.Revoke(header["Bearer ".Length..].Trim());
    return Results.Ok();
});

// ---- KnowledgeHub API + image assets. ----------------------------------------------------------
app.MapKnowledgeHubApi();
app.MapKnowledgeHubAssets();

app.MapFallbackToFile("index.html");

// ---- Seeding. ------------------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var userSeed = await scope.ServiceProvider.GetRequiredService<DemoUserSeeder>().SeedIfEmptyAsync();
    if (!userSeed.Ok) Log.Warning("Demo user seeding did not complete cleanly.");

    var contentSeeder = scope.ServiceProvider.GetRequiredService<KnowledgeHubContentSeeder>();
    var contentSeed = await contentSeeder.SeedSampleContentIfEmptyAsync(new Dictionary<string, string[]>
    {
        ["documentacion-tecnica"] = new[] { DemoPermissions.RolePrefix + "Editor" },
        ["produccion"] = new[] { DemoPermissions.RolePrefix + "Produccion" },
        ["oficinas"] = new[] { DemoPermissions.RolePrefix + "Oficinas" }
    });
    if (!contentSeed.Ok) Log.Warning("KnowledgeHub content seeding did not complete cleanly.");
}

app.Run();

internal sealed record LoginRequest(string UserName, string Password);

internal sealed record LoginResponse(string? Token, string? Error);
