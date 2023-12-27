using Common.Messaging;

namespace GameInterface.Services.MapEvents.Messages
{
    /// <summary>
    /// Event to update game interface when to start a battle
    /// </summary>
    public record StartBattle : ICommand
    {
        public string attackerPartyId { get; }
        public string defenderPartyId { get; }

        public StartBattle(string attackerPartyId, string defenderPartyId)
        {
            this.attackerPartyId = attackerPartyId;
            this.defenderPartyId = defenderPartyId;
        }
    }
}