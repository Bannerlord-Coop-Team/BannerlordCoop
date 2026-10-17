using Common;
using Common.Messaging;
using Common.Network;
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
