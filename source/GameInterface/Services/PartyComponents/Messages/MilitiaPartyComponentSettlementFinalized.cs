using Common.Messaging;
using GameInterface.Services.PartyComponents.Messages;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Messages;

    internal record MilitiaPartyComponentSettlementFinalized(MilitiaPartyComponent Instance) : IEvent
    {
        public MilitiaPartyComponent Instance { get; } = Instance;

    }
