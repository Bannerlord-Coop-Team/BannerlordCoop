using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(PartyUpgraderCampaignBehavior))]
internal class DisablePartyUpgraderCampaignBehavior
{
    [HarmonyPatch(nameof(PartyUpgraderCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
