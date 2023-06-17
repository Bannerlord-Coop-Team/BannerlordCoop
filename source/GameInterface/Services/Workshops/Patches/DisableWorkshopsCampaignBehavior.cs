using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Characters.Patches;

[HarmonyPatch(typeof(WorkshopsCampaignBehavior))]
internal class DisableWorkshopsCampaignBehavior
{
    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
