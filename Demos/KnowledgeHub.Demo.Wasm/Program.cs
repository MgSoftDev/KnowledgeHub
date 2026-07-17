using KnowledgeHub.Demo.Wasm;
using KnowledgeHub.Demo.Wasm.Auth;
using MgSoftDev.KnowledgeHub.Blazor;
using MgSoftDev.KnowledgeHub.Blazor.EditorTools;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Http.Client;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// Demo HOST client (Blazor WebAssembly). The UI components of the KnowledgeHub RCL run
// unchanged in the browser on top of the Http.Client implementations; the server (hosted)
// runs the core + LiteDB and exposes the API.

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ---- Auth plumbing: bearer token attached to every API request. -------------------------
builder.Services.AddSingleton<TokenHolder>();
builder.Services.AddSingleton(sp => new HttpClient(new BearerTokenHandler(sp.GetRequiredService<TokenHolder>()))
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});
builder.Services.AddSingleton<ClientUserContext>();
builder.Services.AddSingleton<IKnowledgeHubUserContext>(sp => sp.GetRequiredService<ClientUserContext>());

// ---- KnowledgeHub module: HTTP contracts + Blazor UI. -------------------------------------
builder.Services.AddKnowledgeHubHttpClient();
builder.Services.AddKnowledgeHubBlazor(o =>
{
    o.PortalTitle = "📚 KnowledgeHub WASM";
    o.HeaderActionsComponent = typeof(KnowledgeHub.Demo.Wasm.Components.WasmHostLinks);

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

await builder.Build().RunAsync();
