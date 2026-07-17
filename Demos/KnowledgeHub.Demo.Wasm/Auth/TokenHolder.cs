namespace KnowledgeHub.Demo.Wasm.Auth;

/// <summary>In-memory bearer token of the signed-in session (lost on refresh; demo simplicity).</summary>
public sealed class TokenHolder
{
    public string? Token { get; set; }
}

/// <summary>Attaches the bearer token to every outgoing API request.</summary>
public sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly TokenHolder _holder;

    public BearerTokenHandler(TokenHolder holder)
    {
        _holder = holder;
        InnerHandler = new HttpClientHandler();
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_holder.Token))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _holder.Token);
        return base.SendAsync(request, cancellationToken);
    }
}
