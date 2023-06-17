using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;

namespace GameInterface.Services.Barters.Patches;

[HarmonyPatch(typeof(TransferPrisonerBarterBehavior))]
internal class DisableTransferPrisonerBarterBehavior
{
    [HarmonyPatch(nameof(TransferPrisonerBarterBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
