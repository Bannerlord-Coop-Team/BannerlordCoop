using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages
{
    public record NetworkPartyRecruitUnit : IEvent
    {
        public string PartyId { get; }
        public string SettlementId { get; }
        public string HeroId { get; }
        public string CharacterId { get; }
        public int Amount { get; }
        public int BitCode { get; }
        public int RecruitingDetail { get; }

        public NetworkPartyRecruitUnit(string partyId, string settlementId, string heroId, string characterId, int amount, int bitCode, int recruitingDetail)
        {
            PartyId = partyId;
            SettlementId = settlementId;
            HeroId = heroId;
            CharacterId = characterId;
            Amount = amount;
            BitCode = bitCode;
            RecruitingDetail = recruitingDetail;
        }
    }
}
