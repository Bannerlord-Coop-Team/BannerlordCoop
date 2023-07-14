using Common.Messaging;

namespace GameInterface.Services.Villages.Messages
{
    /// <summary>
    /// Local event from game interface that village state has been changed
    /// </summary>
    public record ChangeVillageState : IEvent
    {
        public string VillageId { get; }
        public int NewState { get; }
        public string PartyId { get; }

        public ChangeVillageState(string villageId, int newState, string partyId)
        {
            VillageId = villageId;
            NewState = newState;
            PartyId = partyId;
        }
    }
}
