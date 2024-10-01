using Common.Messaging;

namespace GameInterface.Services.MapEvents.Messages
{
    /// <summary>
    /// Event to update game interface when to end a battle
    /// </summary>
    public record EndBattle : ICommand
    {
        public string partyId { get; }

        public EndBattle(string partyId)
        {
            this.partyId = partyId;
        }
    }
}