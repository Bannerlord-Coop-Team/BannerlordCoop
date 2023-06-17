using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;

namespace GameInterface.Services.Barters.Patches;

[HarmonyPatch(typeof(FiefBarterBehavior))]
internal class DisableFiefBarterBehavior
{
    [HarmonyPatch(nameof(FiefBarterBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
