using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Naval.Notifications.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.ObjectSystem;

namespace Coop.Naval.Notifications.Handlers;

internal class NavalNotificationsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalNotificationsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public NavalNotificationsHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<NotifyFigureheadUnlocked>(Handle_NotifyFigureheadUnlocked);
        messageBroker.Subscribe<NetworkNotifyFigureheadUnlocked>(Handle_NetworkNotifyFigureheadUnlocked);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NotifyFigureheadUnlocked>(Handle_NotifyFigureheadUnlocked);
        messageBroker.Unsubscribe<NetworkNotifyFigureheadUnlocked>(Handle_NetworkNotifyFigureheadUnlocked);
    }

    private void Handle_NotifyFigureheadUnlocked(MessagePayload<NotifyFigureheadUnlocked> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.PlayerHero, out var playerHeroId)) return;

            network.SendAll(new NetworkNotifyFigureheadUnlocked(playerHeroId, obj.What.Figurehead.StringId));
        });
    }

    private void Handle_NetworkNotifyFigureheadUnlocked(MessagePayload<NetworkNotifyFigureheadUnlocked> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.PlayerHeroId, out var playerHero)) return;
            if (playerHero != Hero.MainHero) return;

            if (!objectManager.TryGetObjectWithLogging<Figurehead>(obj.What.FigureheadId, out var figurehead)) return;

            if (Campaign.Current.UnlockedFigureheadsByMainHero.Contains(figurehead)) return;

            // Vanilla OnFigureheadUnlocked shows the unlock message and sets this client's last loot time
            Campaign.Current.UnlockFigurehead(figurehead);
        });
    }
}
