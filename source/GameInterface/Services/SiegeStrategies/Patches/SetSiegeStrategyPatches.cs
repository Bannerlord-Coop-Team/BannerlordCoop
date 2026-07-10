using Common;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeStrategies.Patches;

/// <summary>
/// Keeps siege strategy writes server-authoritative and player-led besieger camps on the player-driven
/// Custom strategy. Both SiegeStrategy properties are AutoSync'd, so a client write would apply locally
/// without replicating and silently diverge. Vanilla keeps a camp on Custom only while its leader is
/// Hero.MainHero — nobody on a dedicated server — so any mid-siege camp join/leave would hand a
/// player-led camp to the scored AI strategies, whose planner then queues its own siege engines
/// (e.g. refilling a slot the player just moved to reserve).
/// </summary>
[HarmonyPatch]
internal class SetSiegeStrategyPatches
{
    [HarmonyPatch(typeof(BesiegerCamp), nameof(BesiegerCamp.SetSiegeStrategy))]
    [HarmonyPrefix]
    private static bool BesiegerCampPrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        return ModInformation.IsServer;
    }

    [HarmonyPatch(typeof(Settlement), nameof(Settlement.SetSiegeStrategy))]
    [HarmonyPrefix]
    private static bool SettlementPrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        return ModInformation.IsServer;
    }

    // Vanilla's Hero.MainHero comparison, with the player test fixed the same way as SetDefaultTacticsPrefix.
    [HarmonyPatch(typeof(BesiegerCamp), nameof(BesiegerCamp.ChangeSiegeStrategyIfNeeded))]
    [HarmonyPrefix]
    private static bool ChangeSiegeStrategyIfNeededPrefix(BesiegerCamp __instance)
    {
        if (ModInformation.IsClient) return false;

        return __instance._leaderParty?.LeaderHero?.IsPlayerHero() != true;
    }

    // A save written before the ChangeSiegeStrategyIfNeeded fix can carry an AI strategy on a
    // player-led camp; put it back on Custom before the planner acts on it.
    [HarmonyPatch(typeof(SiegeEvent), nameof(SiegeEvent.AdvanceStrategy))]
    [HarmonyPrefix]
    private static void AdvanceStrategyPrefix(ISiegeEventSide siegeEventSide)
    {
        if (ModInformation.IsClient) return;

        if (siegeEventSide is BesiegerCamp camp
            && camp.SiegeStrategy != DefaultSiegeStrategies.Custom
            && camp._leaderParty?.LeaderHero?.IsPlayerHero() == true)
        {
            camp.SetSiegeStrategy(DefaultSiegeStrategies.Custom);
        }
    }
}
