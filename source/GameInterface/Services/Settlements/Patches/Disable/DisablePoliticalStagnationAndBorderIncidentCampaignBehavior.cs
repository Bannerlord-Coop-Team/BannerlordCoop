using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Settlements.Patches.Disable;


[HarmonyPatch(typeof(PoliticalStagnationAndBorderIncidentCampaignBehavior))]
internal class DisablePoliticalStagnationAndBorderIncidentCampaignBehavior
{
    [HarmonyPatch(nameof(PoliticalStagnationAndBorderIncidentCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
