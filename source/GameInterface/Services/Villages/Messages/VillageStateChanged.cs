using Common.Messaging;

namespace GameInterface.Services.Villages.Messages
{
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
