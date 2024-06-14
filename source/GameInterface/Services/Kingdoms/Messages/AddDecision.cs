using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;

namespace GameInterface.Services.Kingdoms.Messages
{
    /// <summary>
    /// Event that is handled on client side, when Server sends NetworkAddDecision message to clients.
    /// </summary>
    public class AddDecision: ICommand
    {
        public string KingdomId { get; }
        public KingdomDecisionData Data { get; }
        public bool IgnoreInfluenceCost { get; }

        public AddDecision(string kingdomId, KingdomDecisionData data, bool ignoreInfluenceCost)
        {
            KingdomId = kingdomId;
            Data = data;
            IgnoreInfluenceCost = ignoreInfluenceCost;
        }
    }
}
