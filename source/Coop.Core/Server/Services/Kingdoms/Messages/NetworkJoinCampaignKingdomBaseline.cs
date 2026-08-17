using Common.Messaging;
using ProtoBuf;
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

    /// <summary>
    /// Authoritative Kingdom state baseline for a joining client.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public readonly struct NetworkJoinCampaignKingdomBaseline : IMessage
    {
        [ProtoMember(1)]
        public readonly PendingAllianceOfferBaseline[] PendingAllianceOffers;

        public NetworkJoinCampaignKingdomBaseline(PendingAllianceOfferBaseline[] pendingAllianceOffers = null)
        {
            PendingAllianceOffers = pendingAllianceOffers ?? Array.Empty<PendingAllianceOfferBaseline>();
        }
    }
}