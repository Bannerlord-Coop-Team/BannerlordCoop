using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages
{
    /// <summary>
    ///  event to become hostile to game interface for party
    /// </summary>
    public record PartyBeHostile : IEvent
    {
        public string AttackerPartyId { get; }
        public string DefenderPartyId { get; }
        public float Value { get; }

        public PartyBeHostile(string attackerPartyId, string defenderPartyId, float value)
        {
            AttackerPartyId = attackerPartyId;
            DefenderPartyId = defenderPartyId;
            Value = value;
        }
    }
}