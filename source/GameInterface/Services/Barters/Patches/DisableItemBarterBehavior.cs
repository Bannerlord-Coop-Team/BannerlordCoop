using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;

namespace GameInterface.Services.Barters.Patches;

[HarmonyPatch(typeof(ItemBarterBehavior))]
internal class DisableItemBarterBehavior
{
    [HarmonyPatch(nameof(ItemBarterBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
