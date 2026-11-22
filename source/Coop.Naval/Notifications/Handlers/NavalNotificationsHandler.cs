using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Naval.Notifications.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;
using TaleWorlds.Localization;

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

        messageBroker.Subscribe<NotifyShipsGoldRecovered>(Handle_NotifyShipsGoldRecovered);
        messageBroker.Subscribe<NetworkNotifyShipsGoldRecovered>(Handle_NetworkNotifyShipsGoldRecovered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NotifyFigureheadUnlocked>(Handle_NotifyFigureheadUnlocked);
        messageBroker.Unsubscribe<NetworkNotifyFigureheadUnlocked>(Handle_NetworkNotifyFigureheadUnlocked);

        messageBroker.Unsubscribe<NotifyShipsGoldRecovered>(Handle_NotifyShipsGoldRecovered);
        messageBroker.Unsubscribe<NetworkNotifyShipsGoldRecovered>(Handle_NetworkNotifyShipsGoldRecovered);
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

    private void Handle_NotifyShipsGoldRecovered(MessagePayload<NotifyShipsGoldRecovered> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Clan, out var clanId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.PartyOwner, out var partyOwnerId)) return;

            network.SendAll(new NetworkNotifyShipsGoldRecovered(clanId, partyOwnerId, obj.What.Gold));
        });
    }

    private void Handle_NetworkNotifyShipsGoldRecovered(MessagePayload<NetworkNotifyShipsGoldRecovered> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(obj.What.ClanId, out var clan)) return;
            if (clan != Clan.PlayerClan) return;

            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.PartyOwnerId, out var partyOwner)) return;

            // Same message vanilla RecoverGoldFromRemainingShipsAfterDistribution shows for Clan.PlayerClan
            MBTextManager.SetTextVariable("GOLD_AMOUNT", obj.What.Gold);
            MBTextManager.SetTextVariable("LEADER_NAME", partyOwner.Name, false);
            MBTextManager.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"6\">", false);
            MBInformationManager.AddQuickInformation(
                new TextObject("{=YaSnA9j0}{LEADER_NAME}'s party has disbanded. You recovered {GOLD_AMOUNT}{GOLD_ICON} from its ships."),
                0,
                null,
                null,
                string.Empty);
        });
    }
}
