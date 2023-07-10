using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(LordDefectionCampaignBehavior))]
internal class DisableLordDefectionCampaignBehavior
{
    [HarmonyPatch(nameof(LordDefectionCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
