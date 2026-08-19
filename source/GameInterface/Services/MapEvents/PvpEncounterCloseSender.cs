using Common.Network;
using GameInterface.Services.MapEvents.Messages;

namespace GameInterface.Services.MapEvents;

internal static class PvpEncounterCloseSender
{
    public static void Send(
        INetwork network,
        string[] playerPartyIds,
        string surrenderedPartyId = null,
        string mapEventId = null)
    {
        if (playerPartyIds == null || playerPartyIds.Length == 0)
            return;

        var message = new NetworkClosePvpEncounter(playerPartyIds, surrenderedPartyId, mapEventId);
        network.SendAll(message);
    }
}
