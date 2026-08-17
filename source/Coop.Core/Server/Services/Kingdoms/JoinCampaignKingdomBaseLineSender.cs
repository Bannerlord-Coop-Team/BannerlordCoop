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
    private readonly IPeaceOfferPendingCapturer peaceOfferPendingCapturer;

    public JoinCampaignKingdomBaseLineSender(
        INetwork network,
        IAllianceOfferPendingCapturer allianceOfferPendingCapturer,
        IPeaceOfferPendingCapturer peaceOfferPendingCapturer)
    {
        this.network = network;
        this.allianceOfferPendingCapturer = allianceOfferPendingCapturer;
        this.peaceOfferPendingCapturer = peaceOfferPendingCapturer;
    }

    public void Send(NetPeer peer)
    {
        network.SendImmediate(
            peer, new NetworkJoinCampaignKingdomBaseline(allianceOfferPendingCapturer.Capture(),
            peaceOfferPendingCapturer.Capture()));
    }
}