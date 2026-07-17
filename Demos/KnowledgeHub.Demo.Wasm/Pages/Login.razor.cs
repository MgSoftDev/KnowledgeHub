using System.Net.Http.Json;
using KnowledgeHub.Demo.Wasm.Auth;
using MgSoftDev.KnowledgeHub;
using MgSoftDev.PrismPlus.Returning.Commands;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;
using Microsoft.AspNetCore.Components;

namespace KnowledgeHub.Demo.Wasm.Pages;

public partial class Login : ComponentBase
{
    [Inject] private HttpClient Http { get; set; } = null!;
    [Inject] private TokenHolder Tokens { get; set; } = null!;
    [Inject] private ClientUserContext User { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public bool Wait { get; set; }

    private sealed record LoginRequest(string UserName, string Password);
    private sealed record LoginResponse(string? Token, string? Error);

    public AsyncReturningCommand LoginCommand =>
        field ??= new AsyncReturningCommand(async () =>
        {
            ErrorMessage = string.Empty;

            var response = await Http.PostAsJsonAsync("/auth/login", new LoginRequest(UserName, Password));
            if (!response.IsSuccessStatusCode)
                return Returning.Unfinished("Usuario o contraseña incorrectos", UnfinishedInfo.NotifyType.Warning);

            var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (payload?.Token is not { Length: > 0 } token)
                return Returning.Unfinished(payload?.Error ?? "Usuario o contraseña incorrectos", UnfinishedInfo.NotifyType.Warning);

            Tokens.Token = token;
            var refresh = await User.RefreshAsync(Http);
            if (!refresh.Ok) return refresh;

            Nav.NavigateTo(KnowledgeHubRoutes.Home);
            return Returning.Success();
        }, () => !Wait && !string.IsNullOrWhiteSpace(UserName))
        .StartAction(() => Wait = true)
        .EndAction(result =>
        {
            Wait = false;
            if (!result.Ok)
                ErrorMessage = result.UnfinishedInfo?.Title ?? "No se pudo iniciar sesión.";
            StateHasChanged();
        });
}
