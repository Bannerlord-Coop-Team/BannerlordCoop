using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Networking half of the Village Needs Grain Seeds weekly grain-price broadcast - see
/// <see cref="Patches.HeadmanNeedsGrainPriceCachePatches"/> for the full mechanism doc comment. Routes a
/// genuine server-side recompute to every connected client, and applies a received value directly onto the
/// client's own local <see cref="HeadmanNeedsGrainIssueBehavior"/> singleton instance.
/// </summary>
internal class HeadmanNeedsGrainPriceHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public HeadmanNeedsGrainPriceHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<HeadmanGrainPriceCached>(Handle_HeadmanGrainPriceCached);
        messageBroker.Subscribe<NetworkHeadmanGrainPriceUpdated>(Handle_NetworkHeadmanGrainPriceUpdated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<HeadmanGrainPriceCached>(Handle_HeadmanGrainPriceCached);
        messageBroker.Unsubscribe<NetworkHeadmanGrainPriceUpdated>(Handle_NetworkHeadmanGrainPriceUpdated);
    }

    private void Handle_HeadmanGrainPriceCached(MessagePayload<HeadmanGrainPriceCached> payload)
    {
        if (ModInformation.IsClient) return;

        network.SendAll(new NetworkHeadmanGrainPriceUpdated(payload.What.Price));
    }

    private void Handle_NetworkHeadmanGrainPriceUpdated(MessagePayload<NetworkHeadmanGrainPriceUpdated> payload)
    {
        if (ModInformation.IsServer) return;

        var price = payload.What.Price;
        GameThread.RunSafe(() =>
        {
            var behavior = Campaign.Current?.GetCampaignBehavior<HeadmanNeedsGrainIssueBehavior>();
            if (behavior == null) return;

            behavior._averageGrainPriceInCalradia = price;
        });
    }
}
