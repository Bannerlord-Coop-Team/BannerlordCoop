using Common.Messaging;

namespace GameInterface.Services.Villages.Messages
{
    /// <summary>
    /// Event to game interface for changing village state
    /// </summary>
    public record VillageStateChanged : IEvent
    {
        public string VillageId { get; }
        public int NewState { get; }
        public string PartyId { get; }

        public VillageStateChanged(string villageId, int newState, string partyId)
        {
            VillageId = villageId;
            NewState = newState;
            PartyId = partyId;
        }
    }
}
