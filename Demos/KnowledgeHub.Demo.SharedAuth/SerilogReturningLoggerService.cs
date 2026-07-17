using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Helper;
using MgSoftDev.ReturningCore.Logger;
using Serilog;
using Serilog.Events;

namespace KnowledgeHub.Demo.SharedAuth;

/// <summary>
/// Bridges the Returning library's logging to Serilog. Assigned to
/// <see cref="ReturningLogger.LoggerService"/> at startup so every <c>.SaveLog()</c> /
/// <c>saveLog: true</c> call across the app is written to the Serilog sinks.
/// </summary>
public sealed class SerilogReturningLoggerService : IReturningLoggerService
{
    /// <summary>Default event source tag used when a call does not supply one.</summary>
    public object EventSource { get; set; } = "KnowledgeHubDemo";

    private static LogEventLevel Map(ReturningEnums.LogLevel level) => level switch
    {
        ReturningEnums.LogLevel.Trace => LogEventLevel.Verbose,
        ReturningEnums.LogLevel.Debug => LogEventLevel.Debug,
        ReturningEnums.LogLevel.Info => LogEventLevel.Information,
        ReturningEnums.LogLevel.Warn => LogEventLevel.Warning,
        ReturningEnums.LogLevel.Error => LogEventLevel.Error,
        ReturningEnums.LogLevel.Fatal => LogEventLevel.Fatal,
        _ => LogEventLevel.Information
    };

    public bool SaveLog(Returning returning, ReturningEnums.LogLevel logLevel, object eventSource, string logName)
    {
        if (returning.ErrorInfo is not null) return SaveLog(returning.ErrorInfo, logLevel, eventSource, logName);
        if (returning.UnfinishedInfo is not null) return SaveLog(returning.UnfinishedInfo, logLevel, eventSource, logName);
        return true;
    }

    public bool SaveLog(ErrorInfo errorInfo, ReturningEnums.LogLevel logLevel, object eventSource, string logName)
    {
        Log.Write(Map(logLevel),
            "[Returning:{LogName}] Error: {Message} (code={ErrorCode}) at {Member} {File}:{Line} {Exception}",
            logName, errorInfo.ErrorMessage, errorInfo.ErrorCode, errorInfo.MemberName, errorInfo.FilePath,
            errorInfo.LineNumber, errorInfo.TryException?.ToString());
        return true;
    }

    public bool SaveLog(UnfinishedInfo unfinished, ReturningEnums.LogLevel logLevel, object eventSource, string logName)
    {
        Log.Write(Map(logLevel),
            "[Returning:{LogName}] Unfinished {Type}: {Title} — {Message} (code={ErrorCode})",
            logName, unfinished.Type, unfinished.Title, unfinished.Mensaje, unfinished.ErrorCode);
        return true;
    }

    public bool SaveLog(string errorMessage, object parameters, Exception tryException, string errorCode,
        ReturningEnums.LogLevel logLevel, object eventSource, string logName, string memberName, string filePath, int lineNumber)
    {
        Log.Write(Map(logLevel), tryException,
            "[Returning:{LogName}] {Message} (code={ErrorCode}) at {Member} {File}:{Line}",
            logName, errorMessage, errorCode, memberName, filePath, lineNumber);
        return true;
    }

    public Task<bool> SaveLogAsync(Returning returning, ReturningEnums.LogLevel logLevel, object eventSource, string logName)
        => Task.FromResult(SaveLog(returning, logLevel, eventSource, logName));

    public Task<bool> SaveLogAsync(ErrorInfo errorInfo, ReturningEnums.LogLevel logLevel, object eventSource, string logName)
        => Task.FromResult(SaveLog(errorInfo, logLevel, eventSource, logName));

    public Task<bool> SaveLogAsync(UnfinishedInfo unfinished, ReturningEnums.LogLevel logLevel, object eventSource, string logName)
        => Task.FromResult(SaveLog(unfinished, logLevel, eventSource, logName));

    public Task<bool> SaveLogAsync(string errorMessage, object parameters, Exception tryException, string errorCode,
        ReturningEnums.LogLevel logLevel, object eventSource, string logName, string memberName, string filePath, int lineNumber)
        => Task.FromResult(SaveLog(errorMessage, parameters, tryException, errorCode, logLevel, eventSource, logName, memberName, filePath, lineNumber));
}
