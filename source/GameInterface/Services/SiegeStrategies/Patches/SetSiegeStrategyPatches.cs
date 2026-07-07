using Common;
using GameInterface.Policies;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeStrategies.Patches;

/// <summary>
/// Blocks client-side siege strategy writes. Both SiegeStrategy properties are AutoSync'd, so a client
/// write would apply locally without replicating and silently diverge; the server sets the strategy
/// (default tactics, or Custom when a player build order arrives) and the property sync carries it out.
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
}
