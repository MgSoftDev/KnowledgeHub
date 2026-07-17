using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Helper;
using MgSoftDev.ReturningCore.Logger;

namespace KnowledgeHub.ParityHarness;

/// <summary>
/// Minimal Returning logger for the harness: technical errors go to the console; Unfinished
/// results are expected business outcomes in the parity script, so they stay silent.
/// </summary>
public sealed class ConsoleReturningLoggerService : IReturningLoggerService
{
    public object EventSource { get; set; } = "ParityHarness";

    public bool SaveLog(Returning returning, ReturningEnums.LogLevel logLevel, object eventSource, string logName)
    {
        if (returning.ErrorInfo is not null) return SaveLog(returning.ErrorInfo, logLevel, eventSource, logName);
        return true;
    }

    public bool SaveLog(ErrorInfo errorInfo, ReturningEnums.LogLevel logLevel, object eventSource, string logName)
    {
        Console.WriteLine($"  [LOG:{logLevel}] {errorInfo.ErrorMessage} — {errorInfo.TryException}");
        return true;
    }

    public bool SaveLog(UnfinishedInfo unfinished, ReturningEnums.LogLevel logLevel, object eventSource, string logName) => true;

    public bool SaveLog(string errorMessage, object parameters, Exception tryException, string errorCode,
        ReturningEnums.LogLevel logLevel, object eventSource, string logName, string memberName, string filePath, int lineNumber)
    {
        Console.WriteLine($"  [LOG:{logLevel}] {errorMessage} (code={errorCode}) — {tryException}");
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
