using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Kingdoms.Messages
{
    internal readonly struct GiftSettlementOwnership : IEvent
    {
        public readonly Settlement SettlementToGive;
        public readonly Clan ReceiverClan;

        public GiftSettlementOwnership(Settlement settlementToGive, Clan receiverClan)
        {
            SettlementToGive = settlementToGive;
            ReceiverClan = receiverClan;
        }
    }
}
