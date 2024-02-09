using Common.Messaging;

namespace GameInterface.Services.Kingdoms.Messages
{
    /// <summary>
    /// Event that is handled on client side, when Server sends NetworkRemoveDecision message to clients.
    /// </summary>
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
