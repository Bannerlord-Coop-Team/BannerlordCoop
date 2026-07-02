using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// [Server] Announces when a party's occupancy changes — it entered/left a map event
/// (<c>PartyBase.MapEventSide</c>, which backs <c>MobileParty.MapEvent</c>) or a settlement
/// (<c>MobileParty.CurrentSettlement</c>). A single <see cref="PartyOccupancyChanged"/> covers both so the
/// server can re-evaluate whether every player is now occupied (see the occupancy pause handler). Postfixes so
/// the new state is already applied when the message fires.
/// </summary>
[HarmonyPatch]
internal static class PartyOccupancyPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PartyBase), "set_MapEventSide")]
    private static void MapEventSidePostfix(PartyBase __instance)
    {
        Announce(__instance?.MobileParty);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MobileParty), "set_CurrentSettlement")]
    private static void CurrentSettlementPostfix(MobileParty __instance)
    {
        Announce(__instance);
    }

    private static void Announce(MobileParty party)
    {
        // Server-only (only the server drives time). Settlement/garrison parties on the map-event side have no
        // MobileParty — skip them.
        if (ModInformation.IsClient || party == null)
            return;

        if (!party.IsPlayerParty())
            return;

        MessageBroker.Instance.Publish(party, new PartyOccupancyChanged(party));
    }
}
