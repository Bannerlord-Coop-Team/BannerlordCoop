using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Messages
{
    public record BattleRoundSimulated : IEvent
    {
        public string PartyId { get; }
        public int Side { get; }
        public float Advantage { get; }

        public BattleRoundSimulated(string mapEventPartyStringId, int side, float advantage)
        {
            PartyId = mapEventPartyStringId;
            Side = side;
            Advantage = advantage;
        }
    }
}