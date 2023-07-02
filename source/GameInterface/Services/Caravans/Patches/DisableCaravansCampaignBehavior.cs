using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Caravans.Patches;

[HarmonyPatch(typeof(CaravansCampaignBehavior))]
internal class DisableCaravansCampaignBehavior
{
    [HarmonyPatch(nameof(CaravansCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
