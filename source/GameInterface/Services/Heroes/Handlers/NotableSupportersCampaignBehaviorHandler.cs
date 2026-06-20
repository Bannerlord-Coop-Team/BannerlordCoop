using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Handlers;

internal class NotableSupportersCampaignBehaviorHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<NotableSupportersCampaignBehaviorHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IHeroRelationsInterface heroRelationsInterface;

    public NotableSupportersCampaignBehaviorHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IHeroRelationsInterface heroRelationsInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.heroRelationsInterface = heroRelationsInterface;
        messageBroker.Subscribe<NotableSupportAccepted>(Handle_NotableSupportAccepted);
        messageBroker.Subscribe<AcceptNotableSupport>(Handle_AcceptNotableSupport);
        messageBroker.Subscribe<NotableSupportEndedByAgreement>(Handle_NotableSupportEndedByAgreement);
        messageBroker.Subscribe<EndNotableSupportByAgreement>(Handle_EndNotableSupportByAgreement);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NotableSupportAccepted>(Handle_NotableSupportAccepted);
        messageBroker.Unsubscribe<AcceptNotableSupport>(Handle_AcceptNotableSupport);
        messageBroker.Unsubscribe<NotableSupportEndedByAgreement>(Handle_NotableSupportEndedByAgreement);
        messageBroker.Unsubscribe<EndNotableSupportByAgreement>(Handle_EndNotableSupportByAgreement);
    }

    private void Handle_NotableSupportAccepted(MessagePayload<NotableSupportAccepted> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.Notable, out var notableId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.PlayerClan, out var playerClanId)) return;

        var message = new AcceptNotableSupport(mainHeroId, notableId, playerClanId, obj.What.Cost);
        network.SendAll(message);
    }

    private void Handle_AcceptNotableSupport(MessagePayload<AcceptNotableSupport> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.MainHeroId, out var mainHero)) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.NotableId, out var notable)) return;
        if (!objectManager.TryGetObjectWithLogging<Clan>(obj.What.PlayerClanId, out var playerClan)) return;

        heroRelationsInterface.AcceptNotableSupport(mainHero, notable, playerClan, obj.What.Cost);
    }

    private void Handle_NotableSupportEndedByAgreement(MessagePayload<NotableSupportEndedByAgreement> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Notable, out var notableId)) return;

        var message = new EndNotableSupportByAgreement(notableId);
        network.SendAll(message);
    }

    private void Handle_EndNotableSupportByAgreement(MessagePayload<EndNotableSupportByAgreement> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.NotableId, out var notable)) return;

        heroRelationsInterface.EndNotableSupportByAgreement(notable);
    }
}