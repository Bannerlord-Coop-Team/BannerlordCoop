using Common.Messaging;
using GameInterface.Services.PartyComponents.Messages;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Messages;

    internal record MilitiaPartyComponentSettlementChanged(MilitiaPartyComponent Instance, string SettlementId) : IEvent
    {
        public MilitiaPartyComponent Instance { get; } = Instance;
        
        public string SettlementId { get; } = SettlementId;
    }
