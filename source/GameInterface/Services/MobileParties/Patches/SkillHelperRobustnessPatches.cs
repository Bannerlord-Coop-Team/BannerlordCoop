using Common.Logging;
using Helpers;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using GameInterface.Policies;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Guards against IndexOutOfRangeException in SkillHelper.GetEffectivePartyLeaderForSkill
/// and PartyBaseHelper.GetVisualPartyLeader.
///
/// Root cause: TroopRoster has two separate counters:
///   - TotalManCount = _totalRegulars + _totalHeroes  (sum of all soldiers)
///   - Count         = _count                         (number of distinct character type slots)
///
/// During multiplayer sync, TotalManCount can be synced to non-zero before the actual
/// character data entries (_count) are populated.  Vanilla guards only check
/// TotalManCount > 0, then immediately call GetCharacterAtIndex(0) which throws if
/// _count == 0.
/// </summary>
internal static class TroopRosterLeaderHelper
{
    internal static ILogger Logger = LogManager.GetLogger<SkillHelperRobustnessPatches>();

    internal static bool IsSafeToGetCharacterAtIndex(PartyBase party, TroopRoster memberRoster)
    {
        if (memberRoster != null && memberRoster.TotalManCount > 0 && memberRoster.Count == 0)
        {
            Logger.Warning(
                "TroopRoster desync on party {StringId}: TotalManCount={TotalManCount} but Count=0 - returning null leader to avoid IndexOutOfRangeException",
                party.MobileParty?.StringId ?? party.Name?.ToString() ?? "unknown",
                memberRoster.TotalManCount);
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(SkillHelper), nameof(SkillHelper.GetEffectivePartyLeaderForSkill))]
internal class SkillHelperRobustnessPatches
{
    [HarmonyPrefix]
    private static bool Prefix(PartyBase party, ref CharacterObject __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (party == null)
        {
            __result = null;
            return false;
        }

        if (party.LeaderHero != null)
            return true;

        TroopRoster memberRoster = party.MemberRoster;
        if (memberRoster == null || memberRoster.TotalManCount <= 0 || !TroopRosterLeaderHelper.IsSafeToGetCharacterAtIndex(party, memberRoster))
        {
            __result = null;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(PartyBaseHelper), nameof(PartyBaseHelper.GetVisualPartyLeader))]
internal class PartyBaseHelperRobustnessPatches
{
    [HarmonyPrefix]
    private static bool Prefix(PartyBase party, ref CharacterObject __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (party == null)
        {
            __result = null;
            return false;
        }

        if (party.LeaderHero != null)
            return true;

        TroopRoster memberRoster = party.MemberRoster;
        if (memberRoster == null || memberRoster.TotalManCount <= 0 || !TroopRosterLeaderHelper.IsSafeToGetCharacterAtIndex(party, memberRoster))
        {
            __result = null;
            return false;
        }

        return true;
    }
}
