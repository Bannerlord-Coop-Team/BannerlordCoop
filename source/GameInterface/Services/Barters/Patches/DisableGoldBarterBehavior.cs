using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;

namespace GameInterface.Services.Barters.Patches;

[HarmonyPatch(typeof(GoldBarterBehavior))]
internal class DisableGoldBarterBehavior
{
    [HarmonyPatch(nameof(GoldBarterBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
