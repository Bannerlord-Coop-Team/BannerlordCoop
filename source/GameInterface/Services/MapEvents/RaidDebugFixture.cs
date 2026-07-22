#if DEBUG
using Common.Messaging;
using GameInterface.Services.MapEventSides.Messages;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents;

public static class RaidDebugFixture
{
    public static int DetachNonSettlementDefenders(MapEvent mapEvent)
    {
        var defenderSide = mapEvent.DefenderSide;
        var settlementParty = mapEvent.MapEventSettlement?.Party;
        var detachedCount = 0;

        for (var i = defenderSide._battleParties.Count - 1; i >= 0; i--)
        {
            var mapEventParty = defenderSide._battleParties[i];
            var party = mapEventParty.Party;
            if (party == settlementParty)
                continue;

            defenderSide._battleParties.RemoveAt(i);
            defenderSide._mapEvent.RemoveInvolvedPartyInternal(mapEventParty);
            if (party._mapEventSide == defenderSide)
                party._mapEventSide = null;
            MessageBroker.Instance.Publish(defenderSide, new MapEventPartyRemoved(defenderSide, mapEventParty));
            detachedCount++;
        }

        return detachedCount;
    }
}
#endif
