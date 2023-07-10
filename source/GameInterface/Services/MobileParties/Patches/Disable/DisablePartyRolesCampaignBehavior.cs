using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(PartyRolesCampaignBehavior))]
internal class DisablePartyRolesCampaignBehavior
{
    [HarmonyPatch(nameof(PartyRolesCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
