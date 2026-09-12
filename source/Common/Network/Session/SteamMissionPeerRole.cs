namespace Common.Network.Session;

/// <summary>The deterministic connection role one client takes for a pairwise Steam mission link.</summary>
public enum SteamMissionPeerRole
{
    Unavailable,
    Listen,
    Connect,
}

/// <summary>Chooses one initiator for each pair so two Steam loopback bridges are never created.</summary>
public static class SteamMissionPeerRoles
{
    public static SteamMissionPeerRole Resolve(ulong localSteamId, ulong remoteSteamId)
    {
        if (localSteamId == 0 || remoteSteamId == 0 || localSteamId == remoteSteamId)
            return SteamMissionPeerRole.Unavailable;

        return localSteamId < remoteSteamId
            ? SteamMissionPeerRole.Listen
            : SteamMissionPeerRole.Connect;
    }
}
