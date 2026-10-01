using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Messages;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Players.Handlers;

internal class HermitInteractionHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionInteractionsPlayerDataInterface sessionInteractionsPlayerDataInterface;

    public HermitInteractionHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionInteractionsPlayerDataInterface sessionInteractionsPlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionInteractionsPlayerDataInterface = sessionInteractionsPlayerDataInterface;

        messageBroker.Subscribe<UpdateHasMetHermit>(Handle_UpdateHasMetHermit);
        messageBroker.Subscribe<NetworkUpdateHasMetHermit>(Handle_NetworkUpdateHasMetHermit);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<UpdateHasMetHermit>(Handle_UpdateHasMetHermit);
        messageBroker.Unsubscribe<NetworkUpdateHasMetHermit>(Handle_NetworkUpdateHasMetHermit);
    }

    private void Handle_UpdateHasMetHermit(MessagePayload<UpdateHasMetHermit> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;

        network.SendAll(new NetworkUpdateHasMetHermit(mainHeroId, obj.What.HasMetHermit));
    }

    private void Handle_NetworkUpdateHasMetHermit(MessagePayload<NetworkUpdateHasMetHermit> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out _)) return;

            sessionInteractionsPlayerDataInterface.UpdateHasMetHermit(data.MainHeroId, data.HasMetHermit);
        });
    }
}
