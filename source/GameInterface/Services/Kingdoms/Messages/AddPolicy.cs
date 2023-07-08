using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages
{
    public record AddPolicy : IEvent
    {
        public string PolicyId { get; }
        public string KingdomId { get; }

        public AddPolicy(string policyId, string kingdomId)
        {
            this.PolicyId = policyId;
            this.KingdomId = kingdomId;
        }
    }
}