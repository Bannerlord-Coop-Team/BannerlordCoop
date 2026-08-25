using Common.Messaging;
using GameInterface.Services.Kingdoms.Data;
using ProtoBuf;

namespace Coop.Core.Server.Services.Kingdoms.Messages
{
    /// <summary>
    /// Add decision network message.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkAddDecision : ICommand
    {
        [ProtoMember(1)]
        public string KingdomId { get; }
        [ProtoMember(2)]
        public KingdomDecisionData Data { get; }
        [ProtoMember(3, IsRequired = true)]
        public bool IgnoreInfluenceCost { get; }

        [ProtoMember(4)]
        public float RandomNumber { get; }

        /// <summary>
        /// Whether the server queued the decision in <c>Kingdom._unresolvedDecisions</c> instead of resolving
        /// it in place. Null when the server has no answer for this add, the receiver then decides locally.
        /// </summary>
        [ProtoMember(5)]
        public bool? WasQueued { get; }

        public NetworkAddDecision(
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
