using System;

namespace Coop.Core.Server.Services.Telemetry;

/// <summary>Describes one campaign server heartbeat.</summary>
public sealed class ServerTelemetryStatus
{
    public string SessionId { get; }
    public string ModVersion { get; }
    public string Commit { get; }
    public DateTime StartedAt { get; }
    public int PlayerCount { get; }

    public ServerTelemetryStatus(
        string sessionId,
        string modVersion,
        string commit,
        DateTime startedAt,
        int playerCount)
    {
        SessionId = sessionId;
        ModVersion = modVersion;
        Commit = commit;
        StartedAt = startedAt;
        PlayerCount = playerCount;
    }
}
