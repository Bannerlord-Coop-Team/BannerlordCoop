using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(LordWantsRivalCapturedIssueBehavior))]
internal class DisableLordWantsRivalCapturedIssueBehavior
{
    [HarmonyPatch(nameof(LordWantsRivalCapturedIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
