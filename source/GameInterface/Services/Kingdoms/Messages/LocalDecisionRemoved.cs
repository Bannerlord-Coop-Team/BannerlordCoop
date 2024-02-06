using Common.Messaging;

namespace GameInterface.Services.Kingdoms.Messages
{
    public record LocalDecisionRemoved : IEvent
    {
        public string KingdomId { get; }

        public int Index { get; }

        public LocalDecisionRemoved(string kingdomId, int index)
        {
            KingdomId = kingdomId;
            Index = index;
        }
    }
}
