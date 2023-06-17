using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(DiplomaticBartersBehavior))]
internal class DisableDiplomaticBartersBehavior
{
    [HarmonyPatch(nameof(DiplomaticBartersBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
