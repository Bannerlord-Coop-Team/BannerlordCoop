using Common.Network;
using Coop.Core.Server.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms;
using LiteNetLib;
using System.Linq;

namespace Coop.Core.Server.Services.Kingdoms;

public interface IJoinCampaignKingdomBaseLineSender
{
    void Send(NetPeer peer);
}
public sealed class JoinCampaignKingdomBaseLineSender : IJoinCampaignKingdomBaseLineSender
{
    private readonly INetwork network;
    private readonly IAllianceOfferPendingCapturer allianceOfferPendingCapturer;
    private readonly IPeaceOfferPendingCapturer peaceOfferPendingCapturer;
    private readonly IKingdomDecisionVoteManager kingdomDecisionVoteManager;

    public JoinCampaignKingdomBaseLineSender(
        INetwork network,
        IAllianceOfferPendingCapturer allianceOfferPendingCapturer,
        IPeaceOfferPendingCapturer peaceOfferPendingCapturer,
        IKingdomDecisionVoteManager kingdomDecisionVoteManager)
    {
        this.network = network;
        this.allianceOfferPendingCapturer = allianceOfferPendingCapturer;
        this.peaceOfferPendingCapturer = peaceOfferPendingCapturer;
        this.kingdomDecisionVoteManager = kingdomDecisionVoteManager;
    }

    public void Send(NetPeer peer)
    {
        network.SendImmediate(
            peer, new NetworkJoinCampaignKingdomBaseline(allianceOfferPendingCapturer.Capture(),
            peaceOfferPendingCapturer.Capture(),
            kingdomDecisionVoteManager.CaptureActiveRoundStatuses().ToArray()));
    }
}
