using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages
{
    public record PolicyRemoved : IEvent
    {
        public string PolicyId { get; }
        public string KingdomId { get; }

        public PolicyRemoved(string policyId, string kingdomId)
        {
            this.PolicyId = policyId;
            this.KingdomId = kingdomId;
        }
    }
}