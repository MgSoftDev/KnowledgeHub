using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;

namespace MgSoftDev.KnowledgeHub.Transport;

/// <summary>Bidirectional mapping between Returning results and their wire form.</summary>
public static class ReturningTransport
{
    // ---------------------------------------------------------------- Server side (to wire)

    public static ApiResult<T> ToApi<T>(this Returning<T> result) => new()
    {
        Ok = result.Ok,
        Value = result.Ok ? result.Value : default,
        Unfinished = ToApi(result.UnfinishedInfo),
        ErrorMessage = result.ErrorInfo?.ErrorMessage
    };

    public static ApiResult<List<T>> ToApi<T>(this ReturningList<T> result) => new()
    {
        Ok = result.Ok,
        Value = result.Ok ? result.Value : default,
        Unfinished = ToApi(result.UnfinishedInfo),
        ErrorMessage = result.ErrorInfo?.ErrorMessage
    };

    /// <summary>For plain Returning; Value carries nothing (bool placeholder).</summary>
    public static ApiResult<bool> ToApi(this Returning result) => new()
    {
        Ok = result.Ok,
        Value = result.Ok,
        Unfinished = ToApi(result.UnfinishedInfo),
        ErrorMessage = result.ErrorInfo?.ErrorMessage
    };

    private static ApiUnfinished? ToApi(UnfinishedInfo? unfinished) =>
        unfinished is null
            ? null
            : new ApiUnfinished
            {
                Title = unfinished.Title,
                Message = unfinished.Mensaje,
                NotifyType = unfinished.Type.ToString()
            };

    // ---------------------------------------------------------------- Client side (from wire)

    public static Returning<T> ToReturning<T>(this ApiResult<T>? api)
    {
        if (api is null)
            return Returning.Error("Empty API response");
        if (api.Ok)
            return Returning.Success(api.Value!);
        if (api.Unfinished is { } unfinished)
            return Returning.Unfinished(unfinished.Title, unfinished.Message ?? string.Empty, ParseType(unfinished.NotifyType));
        return Returning.Error(api.ErrorMessage ?? "Error remoto");
    }

    public static ReturningList<T> ToReturningList<T>(this ApiResult<List<T>>? api)
    {
        if (api is null)
            return Returning.Error("Empty API response");
        if (api.Ok)
            return Returning.Success(api.Value ?? new List<T>());
        if (api.Unfinished is { } unfinished)
            return Returning.Unfinished(unfinished.Title, unfinished.Message ?? string.Empty, ParseType(unfinished.NotifyType));
        return Returning.Error(api.ErrorMessage ?? "Error remoto");
    }

    public static Returning ToPlainReturning(this ApiResult<bool>? api)
    {
        if (api is null)
            return Returning.Error("Empty API response");
        if (api.Ok)
            return Returning.Success();
        if (api.Unfinished is { } unfinished)
            return Returning.Unfinished(unfinished.Title, unfinished.Message ?? string.Empty, ParseType(unfinished.NotifyType));
        return Returning.Error(api.ErrorMessage ?? "Error remoto");
    }

    private static UnfinishedInfo.NotifyType ParseType(string type) =>
        Enum.TryParse<UnfinishedInfo.NotifyType>(type, ignoreCase: true, out var parsed)
            ? parsed
            : UnfinishedInfo.NotifyType.Warning;
}
