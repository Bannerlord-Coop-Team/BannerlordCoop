using System;
using System.Text.Json.Serialization;

namespace Coop.Core.Server.Services.Telemetry;

/// <summary>Describes one campaign server heartbeat.</summary>
public sealed class ServerTelemetryStatus
{
    [JsonPropertyName("p_session_id")]
    public string SessionId { get; }

    [JsonPropertyName("p_mod_version")]
    public string ModVersion { get; }

    [JsonPropertyName("p_commit_hash")]
    public string Commit { get; }

    [JsonPropertyName("p_started_at")]
    public DateTime StartedAt { get; }

    [JsonPropertyName("p_player_count")]
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
