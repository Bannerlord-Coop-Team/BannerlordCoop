using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(PartyUpgraderCampaignBehavior))]
internal class DisablePartyUpgraderCampaignBehavior
{
    [HarmonyPatch(nameof(PartyUpgraderCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
