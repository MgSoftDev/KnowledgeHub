using System.Security.Claims;
using KnowledgeHub.Demo.BlazorServer.Auth;
using KnowledgeHub.Demo.BlazorServer.Components;
using KnowledgeHub.Demo.SharedAuth;
using MgSoftDev.KnowledgeHub;
using MgSoftDev.KnowledgeHub.AspNetCore;
using MgSoftDev.KnowledgeHub.Blazor;
using MgSoftDev.KnowledgeHub.Blazor.EditorTools;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Seeding;
using MgSoftDev.KnowledgeHub.Storage.LiteDb;
using MgSoftDev.ReturningCore.Logger;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Serilog;

// Demo HOST application (Blazor Server + LiteDB). One server, many browsers: every circuit
// gets its own scope, so IKnowledgeHubUserContext is scoped per signed-in user.

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
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KnowledgeHubDemoServer");
Directory.CreateDirectory(dataFolder);

// ---- Blazor Server + cookie auth. --------------------------------------------------------
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/login";
        o.Cookie.Name = "KnowledgeHubDemoServer";
    });
builder.Services.AddAuthorization();

// ---- Demo HOST plumbing: its own users/roles in its own LiteDB file. ----------------------
builder.Services.AddDemoAuth(Path.Combine(dataFolder, "demo-auth.db"));
builder.Services.AddScoped<IKnowledgeHubUserContext, ServerUserContext>();

// ---- KnowledgeHub module: LiteDB store + core + disk cache + Blazor UI. -------------------
builder.Services.AddKnowledgeHubLiteDbStore(Path.Combine(dataFolder, "demo-knowledgehub.db"));
builder.Services.AddKnowledgeHubCore(o => o.PublicAssetsBaseUrl = "/kh/assets");
builder.Services.AddKnowledgeHubFileImageCache(Path.Combine(dataFolder, "cache"));
builder.Services.AddKnowledgeHubBlazor(o =>
{
    o.PortalTitle = "📚 KnowledgeHub Server";
    o.HeaderActionsComponent = typeof(ServerHostLinks);

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

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();

// ---- KnowledgeHub image assets (hash-addressed, immutable browser caching). --------------
app.MapKnowledgeHubAssets();

// ---- Cookie sign-in/out (plain HTTP endpoints: cookies cannot be set from a circuit). -----
app.MapPost("/auth/login", async (HttpContext http, DemoAuthService auth) =>
{
    var form = await http.Request.ReadFormAsync();
    var userName = form["userName"].ToString();
    var password = form["password"].ToString();

    var result = await auth.LoginAsync(userName, password);
    if (!result.OkNotNull)
        return Results.Redirect("/login?error=1");

    var user = result.Value!;
    var claims = new List<Claim>
    {
        new(ClaimTypes.Name, user.UserName),
        new("FullName", user.FullName)
    };
    claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
    return Results.Redirect(KnowledgeHubRoutes.Home);
}).DisableAntiforgery(); // demo simplicity; production hosts should keep antiforgery on

app.MapGet("/auth/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(KnowledgeHubAssemblyMarker).Assembly,
        typeof(KnowledgeHub.Demo.SharedAuth.Components.HostLinks).Assembly);

// ---- Seeding. ------------------------------------------------------------------------------
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
