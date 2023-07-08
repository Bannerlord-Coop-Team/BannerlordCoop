using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages
{
    public record PolicyAdded : IEvent
    {
        public string PolicyId { get; }
        public string KingdomId { get; }

        public PolicyAdded(string policyId, string kingdomId)
        {
            this.PolicyId = policyId;
            this.KingdomId = kingdomId;
        }
    }
}