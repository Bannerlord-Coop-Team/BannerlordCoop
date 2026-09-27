using GameInterface.Services.Tournaments.Data;
using System;
using TaleWorlds.Library;

namespace Missions.Tournaments;

public static class TournamentDamageAuthority
{
    private const int MaximumInflictedDamage = 2000;

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

    // Vanilla skips the multiplier for shield-blocked hits and fall damage.
    public static bool ShouldApplyPlayerReceivedDamage(
        TournamentContestantData contestant,
        string sourceControllerId,
        bool attackBlockedWithShield,
        bool isFallDamage)
    {
        if (contestant == null || !contestant.IsHuman || contestant.IsReplaced) return false;
        if (string.IsNullOrEmpty(contestant.ControllerId) ||
            contestant.ControllerId == sourceControllerId) return false;
        return !attackBlockedWithShield && !isFallDamage;
    }

    public static int ScalePlayerReceivedDamage(int inflictedDamage, float multiplier)
        => MBMath.ClampInt(
            TaleWorlds.Library.MathF.Ceiling(inflictedDamage * multiplier),
            0,
            MaximumInflictedDamage);
}
