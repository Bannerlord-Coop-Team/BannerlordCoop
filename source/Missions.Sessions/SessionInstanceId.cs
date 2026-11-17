using System;

namespace Missions.Sessions;

/// <summary>
/// Instance id of a campaign-free mission session. The <c>session:</c> prefix cannot collide with a map
/// event id (digits) or a location id (<c>settlement|location</c>, split on <c>|</c> by
/// <c>LocationInstanceId.TryGetSettlementId</c>).
/// </summary>
public static class SessionInstanceId
{
    public const string Prefix = "session:";

    public static string Create() => Prefix + Guid.NewGuid().ToString("N");

    public static bool IsSession(string instanceId) =>
        instanceId != null && instanceId.StartsWith(Prefix, StringComparison.Ordinal);
}
