using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Workshops.Patches;

[HarmonyPatch(typeof(WorkshopsCampaignBehavior))]
internal class DisableWorkshopsCampaignBehavior
{
    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}
