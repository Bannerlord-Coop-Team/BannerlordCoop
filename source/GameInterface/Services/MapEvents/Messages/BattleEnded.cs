using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages
{
    /// <summary>
    /// Event from game interface when a battle ends
    /// </summary>
    public record BattleEnded : IEvent
    {
        public string partyId { get; }

        public BattleEnded(string partyId)
        {
            this.partyId = partyId;
        }
    }
}