using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Registry.Messages;
using LiteNetLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain;

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
        messageBroker.Subscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
        messageBroker.Subscribe<RequestHeadmanGrainPrice>(Handle_RequestHeadmanGrainPrice);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<HeadmanGrainPriceCached>(Handle_HeadmanGrainPriceCached);
        messageBroker.Unsubscribe<NetworkHeadmanGrainPriceUpdated>(Handle_NetworkHeadmanGrainPriceUpdated);
        messageBroker.Unsubscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
        messageBroker.Unsubscribe<RequestHeadmanGrainPrice>(Handle_RequestHeadmanGrainPrice);
    }

    private void Handle_AllGameObjectsRegistered(MessagePayload<AllGameObjectsRegistered> payload)
    {
        if (ModInformation.IsClient) network.SendAll(new RequestHeadmanGrainPrice());
    }

    private void Handle_RequestHeadmanGrainPrice(MessagePayload<RequestHeadmanGrainPrice> payload)
    {
        if (ModInformation.IsClient || !(payload.Who is NetPeer peer)) return;
        GameThread.RunSafe(() =>
        {
            var behavior = Campaign.Current.GetCampaignBehavior<HeadmanNeedsGrainIssueBehavior>();
            network.Send(peer, new NetworkHeadmanGrainPriceUpdated(behavior._averageGrainPriceInCalradia));
        }, context: nameof(HeadmanNeedsGrainPriceHandler));
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
