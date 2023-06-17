using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;

namespace GameInterface.Services.Barters.Patches;

/// <summary>
/// This behavior seems to do nothing
/// </summary>
[HarmonyPatch(typeof(MarriageBarterBehavior))]
internal class DisableMarriageBarterBehavior
{
    [HarmonyPatch(nameof(MarriageBarterBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
