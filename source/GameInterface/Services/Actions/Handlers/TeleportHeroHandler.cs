using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Actions.Handlers;

internal class TeleportHeroHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TeleportHeroHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public TeleportHeroHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<HeroTeleported>(Handle_HeroTeleported);
        messageBroker.Subscribe<TeleportHero>(Handle_TeleportHero);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<HeroTeleported>(Handle_HeroTeleported);
        messageBroker.Unsubscribe<TeleportHero>(Handle_TeleportHero);
    }

    private void Handle_HeroTeleported(MessagePayload<HeroTeleported> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;

        string targetSettlementId = null;
        if (obj.What.TargetSettlement != null && !objectManager.TryGetIdWithLogging(obj.What.TargetSettlement, out targetSettlementId)) return;

        string targetPartyId = null;
        if (obj.What.TargetParty != null && !objectManager.TryGetIdWithLogging(obj.What.TargetParty, out targetPartyId)) return;

        var message = new TeleportHero(heroId, targetSettlementId, targetPartyId, obj.What.Detail);
        network.SendAll(message);
    }

    private void Handle_TeleportHero(MessagePayload<TeleportHero> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;

        Settlement targetSettlement = null;
        if (obj.What.TargetSettlementId != null && !objectManager.TryGetObjectWithLogging(obj.What.TargetSettlementId, out targetSettlement)) return;

        MobileParty targetParty = null;
        if (obj.What.TargetPartyId != null && !objectManager.TryGetObjectWithLogging(obj.What.TargetPartyId, out targetParty)) return;

        TeleportHeroAction.ApplyInternal(hero, targetSettlement, targetParty, obj.What.Detail);
    }
}