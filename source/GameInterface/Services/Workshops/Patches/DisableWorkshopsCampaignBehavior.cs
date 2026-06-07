using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Characters.Patches;

[HarmonyPatch(typeof(WorkshopsCampaignBehavior))]
internal class DisableWorkshopsCampaignBehavior
{
    [HarmonyPatch(nameof(WorkshopsCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }
}
