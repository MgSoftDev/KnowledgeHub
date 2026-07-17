using System.Text;
using KnowledgeHub.ParityHarness;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Security;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;
using Microsoft.AspNetCore.Http;

namespace KnowledgeHub.ParityHarness;

/// <summary>
/// Harness-only auth plumbing for the HTTP parity run: the client stamps the current fake
/// user into request headers and the server's scoped user context reads them back. This
/// simulates what a real host does with cookies/tokens, without dragging auth into the test.
/// </summary>
public static class HarnessAuthHeaders
{
    public const string UserHeader = "X-KH-User";
    public const string NameHeader = "X-KH-Name";      // base64(UTF8) — display names carry accents
    public const string PermsHeader = "X-KH-Perms";    // comma-separated
}

/// <summary>Client side: copies the shared HarnessUserContext into every request.</summary>
public sealed class HarnessAuthHandler : DelegatingHandler
{
    private readonly HarnessUserContext _user;

    public HarnessAuthHandler(HarnessUserContext user)
    {
        _user = user;
        InnerHandler = new HttpClientHandler();
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_user.IsAuthenticated)
        {
            request.Headers.Add(HarnessAuthHeaders.UserHeader, _user.UserName);
            request.Headers.Add(HarnessAuthHeaders.NameHeader,
                Convert.ToBase64String(Encoding.UTF8.GetBytes(_user.DisplayName)));
            request.Headers.Add(HarnessAuthHeaders.PermsHeader, string.Join(",", _user.Permissions));
        }
        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>Server side: per-request user context materialized from the harness headers.</summary>
public sealed class HeaderUserContext : IKnowledgeHubUserContext
{
    private readonly IHttpContextAccessor _accessor;

    public HeaderUserContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private string Header(string name) =>
        _accessor.HttpContext?.Request.Headers[name].ToString() ?? string.Empty;

    public bool IsAuthenticated => Header(HarnessAuthHeaders.UserHeader).Length > 0;

    public string UserName => Header(HarnessAuthHeaders.UserHeader);

    public string DisplayName
    {
        get
        {
            var encoded = Header(HarnessAuthHeaders.NameHeader);
            if (encoded.Length == 0) return string.Empty;
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(encoded)); }
            catch (FormatException) { return string.Empty; }
        }
    }

    public IReadOnlyList<string> Permissions
    {
        get
        {
            var raw = Header(HarnessAuthHeaders.PermsHeader);
            return raw.Length == 0 ? Array.Empty<string>() : raw.Split(',', StringSplitOptions.RemoveEmptyEntries);
        }
    }

    public Task<ReturningList<PermissionInfo>> GetPermissionCatalogAsync() =>
        Task.FromResult(ReturningList<PermissionInfo>.Try(() => new List<PermissionInfo>
        {
            new("Docs.Tech", "Documentación técnica"),
            new("Docs.Prod", "Producción"),
            new("Docs.Ofi", "Oficinas")
        }));
}
