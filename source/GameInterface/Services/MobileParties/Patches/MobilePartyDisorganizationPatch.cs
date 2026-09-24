using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.CheckIsDisorganized))]
internal class MobilePartyDisorganizationPatch
{
    // Clients receive the authoritative expiry through _isDisorganized synchronization.
    [HarmonyPrefix]
    private static bool Prefix() => ModInformation.IsServer;
}
