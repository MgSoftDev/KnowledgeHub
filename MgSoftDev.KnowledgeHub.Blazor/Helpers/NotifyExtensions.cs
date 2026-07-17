using MgSoftDev.ReturningCore;
using Radzen;

namespace MgSoftDev.KnowledgeHub.Blazor.Helpers;

/// <summary>
/// Bridges the Returning result pattern to Radzen's <see cref="NotificationService"/>. The
/// MgSoftDev libraries have no UI dependency, so this glue lives in the UI package; hosts can
/// use it for their own pages too.
/// </summary>
public static class NotifyExtensions
{
    public static void ShowSuccess(this NotificationService notify, string message) =>
        notify.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Success,
            Summary = "Éxito",
            Detail = message,
            Duration = 4000
        });

    public static void ShowInfo(this NotificationService notify, string message) =>
        notify.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Info,
            Summary = "Información",
            Detail = message,
            Duration = 4000
        });

    /// <summary>
    /// If the result is not Ok, raises a Radzen notification: a business warning/error from the
    /// UnfinishedInfo, or a generic technical-error message otherwise (details go to the log).
    /// </summary>
    public static void SendNotifyIfNotOk(this ReturningBase result, NotificationService notify, string fallbackTitle)
    {
        if (result.Ok) return;

        if (result.UnfinishedInfo is not null)
        {
            var severity = result.UnfinishedInfo.Type switch
            {
                UnfinishedInfo.NotifyType.Success => NotificationSeverity.Success,
                UnfinishedInfo.NotifyType.Warning => NotificationSeverity.Warning,
                UnfinishedInfo.NotifyType.Error => NotificationSeverity.Error,
                _ => NotificationSeverity.Info
            };
            notify.Notify(new NotificationMessage
            {
                Severity = severity,
                Summary = result.UnfinishedInfo.Title,
                Detail = result.UnfinishedInfo.Mensaje,
                Duration = 5000
            });
            return;
        }

        notify.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Error,
            Summary = fallbackTitle,
            Detail = "Ocurrió un error. Revisa el registro.",
            Duration = 6000
        });
    }
}
