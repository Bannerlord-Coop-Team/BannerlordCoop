using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Event to update game interface when prisoner is taken
    /// </summary>
    public record TakePrisoner : ICommand
    {
        public string PartyId { get; }
        public string CharacterId { get; }
        public bool IsEventCalled { get; }

        public TakePrisoner(string partyId, string characterId, bool isEventCalled)
        {
            PartyId = partyId;
            CharacterId = characterId;
            IsEventCalled = isEventCalled;
        }
    }
}
