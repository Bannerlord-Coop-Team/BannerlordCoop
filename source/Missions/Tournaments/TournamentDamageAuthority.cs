using System;

namespace Missions.Tournaments;

public static class TournamentDamageAuthority
{
    public static bool IsValidOrigin(
        string originControllerId,
        string victimControllerId,
        Guid attackerAgentId,
        string attackerControllerId)
    {
        if (string.IsNullOrEmpty(originControllerId)) return false;
        return attackerAgentId == Guid.Empty
            ? originControllerId == victimControllerId
            : originControllerId == attackerControllerId;
    }
}
