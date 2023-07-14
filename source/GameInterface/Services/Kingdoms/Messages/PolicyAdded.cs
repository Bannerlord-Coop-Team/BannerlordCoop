using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages
{
    /// <summary>
    /// Informs game interface that a policy has been added
    /// </summary>
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