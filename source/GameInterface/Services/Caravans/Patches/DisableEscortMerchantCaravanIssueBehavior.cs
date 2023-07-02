using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Caravans.Patches;

[HarmonyPatch(typeof(EscortMerchantCaravanIssueBehavior))]
internal class DisableEscortMerchantCaravanIssueBehavior
{
    [HarmonyPatch(nameof(EscortMerchantCaravanIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
