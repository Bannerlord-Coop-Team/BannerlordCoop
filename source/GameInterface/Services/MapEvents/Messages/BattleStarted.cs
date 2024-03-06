using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages
{
    /// <summary>
    /// Event from game interface when a battle starts
    /// </summary>
    public record BattleStarted : IEvent
    {
        public string attackerPartyId { get; }
        public string defenderPartyId { get; }

        public BattleStarted(string attackerPartyId, string defenderPartyId)
        {
            this.attackerPartyId = attackerPartyId;
            this.defenderPartyId = defenderPartyId;
        }
    }
}