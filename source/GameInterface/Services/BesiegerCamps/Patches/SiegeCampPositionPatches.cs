using Common;
using Common.Messaging;
using GameInterface.Services.BesiegerCamps.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.BesiegerCamps.Patches;

/// <summary>
/// The camp slot a party takes when it joins a siege is rolled with local RNG inside the BesiegerCamp
/// setter, so the replicated setter re-run lands every machine's copy of the party on a different spot.
/// The server's roll is broadcast and everyone snaps to it; besieger parties sit still, so the regular
/// movement sync never corrects them otherwise.
/// </summary>
[HarmonyPatch(typeof(MobileParty))]
internal class SiegeCampPositionPatches
{
    [HarmonyPatch(nameof(MobileParty.OnPartyJoinedSiegeInternal))]
    [HarmonyPostfix]
    private static void OnPartyJoinedSiegePostfix(MobileParty __instance)
    {
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(__instance, new SiegeCampPositionRolled(__instance, __instance.Position));
    }
}
