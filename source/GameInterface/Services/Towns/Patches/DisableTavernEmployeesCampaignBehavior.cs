using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Towns.Patches;

[HarmonyPatch(typeof(TavernEmployeesCampaignBehavior))]
internal class DisableTavernEmployeesCampaignBehavior
{
    [HarmonyPatch(nameof(TavernEmployeesCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
