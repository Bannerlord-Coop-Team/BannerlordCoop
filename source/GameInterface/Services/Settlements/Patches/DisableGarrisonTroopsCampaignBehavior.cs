using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Settlements.Patches;


[HarmonyPatch(typeof(GarrisonTroopsCampaignBehavior))]
internal class DisableGarrisonTroopsCampaignBehavior
{
    [HarmonyPatch(nameof(GarrisonTroopsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
