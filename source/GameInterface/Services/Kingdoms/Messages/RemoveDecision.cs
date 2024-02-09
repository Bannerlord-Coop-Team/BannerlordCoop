using Common.Messaging;

namespace GameInterface.Services.Kingdoms.Messages
{
    public class RemoveDecision: IEvent
    {
        public string KingdomId { get; }
        public int Index { get; }

        public RemoveDecision(string kingdomId, int index)
        {
            KingdomId = kingdomId;
            Index = index;
        }
    }
}
