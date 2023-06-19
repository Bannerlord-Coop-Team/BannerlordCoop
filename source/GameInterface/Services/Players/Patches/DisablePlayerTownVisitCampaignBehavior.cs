using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior))]
internal class DisablePlayerTownVisitCampaignBehavior
{
    [HarmonyPatch(nameof(PlayerTownVisitCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
