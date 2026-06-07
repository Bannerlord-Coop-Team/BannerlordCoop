using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Settlements.Patches;


[HarmonyPatch(typeof(MilitiasCampaignBehavior))]
internal class DisableMilitiasCampaignBehavior
{
    [HarmonyPatch(nameof(MilitiasCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
