using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Handlers;

internal class PlayerDivorceHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerDivorceRules divorceRules;
    private readonly IPlayerManager playerManager;

    public PlayerDivorceHandler(IMessageBroker messageBroker, IObjectManager objectManager,
        INetwork network, IPlayerDivorceRules divorceRules, IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.divorceRules = divorceRules;
        this.playerManager = playerManager;

        messageBroker.Subscribe<PlayerDivorceRequested>(Handle);
        messageBroker.Subscribe<RequestPlayerDivorce>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerDivorceRequested>(Handle);
        messageBroker.Unsubscribe<RequestPlayerDivorce>(Handle);
    }

    private void Handle(MessagePayload<PlayerDivorceRequested> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.Actor, out var actorId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Spouse, out var spouseId)) return;

        network.SendAll(new RequestPlayerDivorce(actorId, spouseId));
    }

    private void Handle(MessagePayload<RequestPlayerDivorce> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, data.ActorId)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.ActorId, out var actor)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.SpouseId, out var spouse)) return;

            divorceRules.TryApply(actor, spouse, false);
        });
    }
}
