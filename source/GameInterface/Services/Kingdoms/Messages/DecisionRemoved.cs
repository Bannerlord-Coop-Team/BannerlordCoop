using Common.Messaging;

namespace GameInterface.Services.Kingdoms.Messages
{
    /// <summary>
    /// Event that is handled on server side, when Kingdom.RemoveDecision method is called.
    /// </summary>
    public record DecisionRemoved : IEvent
    {
        public string KingdomId { get; }

        public int Index { get; }

        public DecisionRemoved(string kingdomId, int index)
        {
            KingdomId = kingdomId;
            Index = index;
        }
    }
}
