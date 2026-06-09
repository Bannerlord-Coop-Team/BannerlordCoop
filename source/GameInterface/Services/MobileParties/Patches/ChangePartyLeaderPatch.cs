using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Replicates party leader changes from the server. The leader is what gives a party its nameplate and
/// drives its map visual, and nothing else syncs it - so a server-side <c>ChangePartyLeader</c> (e.g. when
/// a captured hero is removed, or restored on release) would otherwise leave the client's party leaderless.
/// </summary>
[HarmonyPatch(typeof(MobileParty))]
internal class ChangePartyLeaderPatch
{
    [HarmonyPatch(nameof(MobileParty.ChangePartyLeader))]
    [HarmonyPrefix]
    private static void Prefix(MobileParty __instance, Hero newLeader)
    {
        // Don't echo a change the client is applying from the server.
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        // The server is authoritative for party leadership; clients apply it via NetworkChangePartyLeader.
        if (!ModInformation.IsServer) return;

        MessageBroker.Instance.Publish(__instance, new PartyLeaderChanged(__instance, newLeader));
    }
}
