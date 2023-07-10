using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(PartyHealCampaignBehavior))]
internal class DisablePartyHealCampaignBehavior
{
    [HarmonyPatch(nameof(PartyHealCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
