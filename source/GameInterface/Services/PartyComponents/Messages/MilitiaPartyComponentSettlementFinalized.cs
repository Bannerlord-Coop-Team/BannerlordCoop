using Common.Messaging;
using GameInterface.Services.PartyComponents.Messages;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Messages;

    public record MilitiaPartyComponentSettlementFinalized : IEvent
    {
        public MilitiaPartyComponent MilitiaPartyComponent { get; }

        public MilitiaPartyComponentSettlementFinalized(MilitiaPartyComponent militiaPartyComponent)
        {
            MilitiaPartyComponent = militiaPartyComponent;
        }
    }
