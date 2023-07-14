using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages
{
    /// <summary>
    /// Informs game interface of a policy being removed
    /// </summary>
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