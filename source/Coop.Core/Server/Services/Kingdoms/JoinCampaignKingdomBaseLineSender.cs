using Common.Network;
using Coop.Core.Server.Services.Kingdoms.Messages;
using LiteNetLib;

namespace Coop.Core.Server.Services.Kingdoms;

public interface IJoinCampaignKingdomBaseLineSender
{
    void Send(NetPeer peer);
}
public sealed class JoinCampaignKingdomBaseLineSender : IJoinCampaignKingdomBaseLineSender
{
    private readonly INetwork network;
    private readonly IAllianceOfferPendingCapturer allianceOfferPendingCapturer;

    public JoinCampaignKingdomBaseLineSender(
        INetwork network,
        IAllianceOfferPendingCapturer allianceOfferPendingCapturer)
    {
        this.network = network;
        this.allianceOfferPendingCapturer = allianceOfferPendingCapturer;
    }

    public void Send(NetPeer peer)
    {
        network.SendImmediate(
            peer, new NetworkJoinCampaignKingdomBaseline(allianceOfferPendingCapturer.Capture()));
    }
}