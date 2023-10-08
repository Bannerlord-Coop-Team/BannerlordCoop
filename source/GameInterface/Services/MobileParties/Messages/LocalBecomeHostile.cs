using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages
{
    /// <summary>
    /// Local event to become hostile
    /// </summary>
    public record LocalBecomeHostile : IEvent
    {
        public string AttackerPartyId { get; }
        public string DefenderPartyId { get; }
        public float Value { get; }

        public LocalBecomeHostile(string attackerPartyId, string defenderPartyId, float value)
        {
            AttackerPartyId = attackerPartyId;
            DefenderPartyId = defenderPartyId;
            Value = value;
        }
    }
}