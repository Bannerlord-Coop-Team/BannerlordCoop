using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Settlements.Patches;


[HarmonyPatch(typeof(PoliticalStagnationAndBorderIncidentCampaignBehavior))]
internal class DisablePoliticalStagnationAndBorderIncidentCampaignBehavior
{
    [HarmonyPatch(nameof(PoliticalStagnationAndBorderIncidentCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
