using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages
{
    /// <summary>
    /// Fires when game interface adds a policy
    /// </summary>
    public record AddPolicy : ICommand
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