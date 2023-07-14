using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages
{
    /// <summary>
    /// Event when a policy is removed from game interface
    /// </summary>
    public record RemovePolicy : ICommand
    {
        public string PolicyId { get; }
        public string KingdomId { get; }

        public RemovePolicy(string policyId, string kingdomId)
        {
            this.PolicyId = policyId;
            this.KingdomId = kingdomId;
        }
    }
}