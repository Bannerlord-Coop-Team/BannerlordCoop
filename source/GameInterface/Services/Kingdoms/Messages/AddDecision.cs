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

        public float RandomNumber { get; }

        /// <summary>
        /// The server's queue-vs-resolve answer, or null when the receiver has to decide locally.
        /// </summary>
        public bool? WasQueued { get; }

        public AddDecision(
            string kingdomId,
            KingdomDecisionData data,
            bool ignoreInfluenceCost,
            float randomNumber,
            bool? wasQueued)
        {
            KingdomId = kingdomId;
            Data = data;
            IgnoreInfluenceCost = ignoreInfluenceCost;
            RandomNumber = randomNumber;
            WasQueued = wasQueued;
        }
    }
}
