using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;

namespace GameInterface.Services.Barters.Patches;

/// <summary>
/// This behavior seems to do nothing
/// </summary>
[HarmonyPatch(typeof(LiftSiegeBarterBehavior))]
internal class DisableLiftSiegeBarterBehavior
{
    [HarmonyPatch(nameof(LiftSiegeBarterBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
