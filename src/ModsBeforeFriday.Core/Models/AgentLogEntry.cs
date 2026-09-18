namespace ModsBeforeFriday.Core.Models;

public enum AgentLogLevel
{
    Error,
    Warn,
    Info,
    Debug,
    Trace,
}

public sealed record AgentLogEntry(DateTimeOffset Timestamp, AgentLogLevel Level, string Message);
