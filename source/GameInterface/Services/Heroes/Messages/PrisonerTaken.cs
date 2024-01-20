using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event sent when a prisoner is taken
    /// </summary>
    public record PrisonerTaken : IEvent
    {
        public string PartyId { get; }
        public string CharacterId { get; }
        public bool IsEventCalled { get; }

        public PrisonerTaken(string partyId, string characterId, bool isEventCalled)
        {
            PartyId = partyId;
            CharacterId = characterId;
            IsEventCalled = isEventCalled;
        }
    }
}
