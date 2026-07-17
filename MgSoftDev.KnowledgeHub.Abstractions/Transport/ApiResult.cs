namespace MgSoftDev.KnowledgeHub.Transport;

/// <summary>
/// Wire format for a Returning result. Returning itself is never serialized (it carries
/// exceptions and internals that must stay server-side); this DTO preserves the semantics
/// the UI needs — most importantly the Unfinished business outcomes (e.g. the publish
/// conflict), which MUST reach the client as warnings, not as HTTP errors. Lives in
/// Abstractions so both Http.Server and the WASM-safe Http.Client share it.
/// </summary>
public sealed class ApiResult<T>
{
    public bool Ok { get; set; }
    public T? Value { get; set; }
    public ApiUnfinished? Unfinished { get; set; }

    /// <summary>Short technical summary. Stack traces stay on the server (logged there).</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>Wire form of UnfinishedInfo. NotifyType matches UnfinishedInfo.NotifyType names.</summary>
public sealed class ApiUnfinished
{
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string NotifyType { get; set; } = "Warning";
}
