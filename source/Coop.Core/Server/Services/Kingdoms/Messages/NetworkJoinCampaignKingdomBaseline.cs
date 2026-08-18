using Common.Messaging;
using ProtoBuf;
using GameInterface.Services.Kingdoms.Data;
using System;

namespace Coop.Core.Server.Services.Kingdoms.Messages
{
    /// <summary> pending alliance offer between two kingdoms.</summary>
    [ProtoContract(SkipConstructor = true)]
    public readonly struct PendingAllianceOfferBaseline
    {
        [ProtoMember(1)]
        public readonly string RequestingKingdomId;

        [ProtoMember(2)]
        public readonly string TargetKingdomId;

        public PendingAllianceOfferBaseline(string requestingKingdomId, string targetKingdomId)
        {
            RequestingKingdomId = requestingKingdomId;
            TargetKingdomId = targetKingdomId;
        }
    }

    ///<summary> pending peace offer between two kingdoms.</summary>
    [ProtoContract(SkipConstructor = true)]
    public readonly struct PendingPeaceOfferBaseline
    {
        [ProtoMember(1)]
        public readonly string RequestingKingdomId;

        [ProtoMember(2)]
        public readonly string TargetKingdomId;

        public PendingPeaceOfferBaseline(string requestingKingdomId, string targetKingdomId)
        {
            RequestingKingdomId = requestingKingdomId;
            TargetKingdomId = targetKingdomId;
        }
    }

    /// <summary>
    /// Authoritative Kingdom state baseline for a joining client.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public readonly struct NetworkJoinCampaignKingdomBaseline : IMessage
    {
        [ProtoMember(1)]
        public readonly PendingAllianceOfferBaseline[] PendingAllianceOffers;

        [ProtoMember(2)]
        public readonly PendingPeaceOfferBaseline[] PendingPeaceOffers;

        [ProtoMember(3)]
        public readonly KingdomDecisionRoundStatusData[] ActiveDecisionRounds;

        public NetworkJoinCampaignKingdomBaseline(
            PendingAllianceOfferBaseline[] pendingAllianceOffers = null,
            PendingPeaceOfferBaseline[] pendingPeaceOffers = null,
            KingdomDecisionRoundStatusData[] activeDecisionRounds = null)
        {
            PendingAllianceOffers = pendingAllianceOffers ?? Array.Empty<PendingAllianceOfferBaseline>();
            PendingPeaceOffers = pendingPeaceOffers ?? Array.Empty<PendingPeaceOfferBaseline>();
            ActiveDecisionRounds = activeDecisionRounds ?? Array.Empty<KingdomDecisionRoundStatusData>();
        }
    }
}
