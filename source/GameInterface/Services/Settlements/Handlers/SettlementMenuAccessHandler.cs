using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Settlements.Messages;
using LiteNetLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Handlers;

internal class SettlementMenuAccessHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private readonly ISettlementMenuAccess settlementMenuAccess;

    public SettlementMenuAccessHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IPlayerManager playerManager,
        IObjectManager objectManager,
        ISettlementMenuAccess settlementMenuAccess)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.playerManager = playerManager;
        this.objectManager = objectManager;
        this.settlementMenuAccess = settlementMenuAccess;

        messageBroker.Subscribe<RequestSettlementMenuAccess>(Handle_RequestSettlementMenuAccess);
        messageBroker.Subscribe<NetworkSettlementMenuAccess>(Handle_NetworkSettlementMenuAccess);
        messageBroker.Subscribe<NetworkSettlementMenusChanged>(Handle_NetworkSettlementMenusChanged);

        messageBroker.Subscribe<PlayerConnectionStateChanged>(Handle_PlayerConnectionStateChanged);
        messageBroker.Subscribe<PartyOccupancyChanged>(Handle_OccupancyChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RequestSettlementMenuAccess>(Handle_RequestSettlementMenuAccess);
        messageBroker.Unsubscribe<NetworkSettlementMenuAccess>(Handle_NetworkSettlementMenuAccess);
        messageBroker.Unsubscribe<NetworkSettlementMenusChanged>(Handle_NetworkSettlementMenusChanged);

        messageBroker.Unsubscribe<PlayerConnectionStateChanged>(Handle_PlayerConnectionStateChanged);
        messageBroker.Unsubscribe<PartyOccupancyChanged>(Handle_OccupancyChanged);
    }

    private void Handle_RequestSettlementMenuAccess(MessagePayload<RequestSettlementMenuAccess> obj)
    {
        if (ModInformation.IsClient) return;

        GameThread.RunSafe(() =>
        {
            var peer = (NetPeer)obj.Who;
            if (!playerManager.TryGetPlayer(peer, out var player)) return;
            var request = obj.What;
            if (!request.Open)
            {
                if (settlementMenuAccess.Release(player.HeroId)) Broadcast();
                return;
            }

            bool granted = SettlementMenuAccess.IsManagedMenu(request.MenuId) &&
                CanUseSettlement(player.HeroId, request.SettlementId) &&
                settlementMenuAccess.TryAcquire(request.SettlementId, request.MenuId, player.HeroId);

            if (granted) Broadcast();
            network.Send(peer, new NetworkSettlementMenuAccess(request.SettlementId, request.MenuId, granted));
        });
    }

    private void Handle_NetworkSettlementMenuAccess(MessagePayload<NetworkSettlementMenuAccess> obj)
    {
        GameThread.RunSafe(() =>
        {
            settlementMenuAccess.OnAccessReceived(obj.What);
        });
    }
        

    private void Handle_NetworkSettlementMenusChanged(MessagePayload<NetworkSettlementMenusChanged> obj)
    {
        GameThread.RunSafe(() =>
        {
            settlementMenuAccess.Update(obj.What.Menus);
        });
    }

    private void Handle_PlayerConnectionStateChanged(MessagePayload<PlayerConnectionStateChanged> obj)
    {
        if (ModInformation.IsClient) return;

        GameThread.RunSafe(() =>
        {
            RemoveUnavailablePlayers();
            // Joining clients also need the currently occupied menus.
            Broadcast();
        });
    }

    private void Handle_OccupancyChanged(MessagePayload<PartyOccupancyChanged> obj)
    {
        if (ModInformation.IsClient) return;

        GameThread.RunSafe(() =>
        {
            if (RemoveUnavailablePlayers()) Broadcast();
        });
    }

    private bool RemoveUnavailablePlayers()
    {
        var connectedHeroes = playerManager.Players.Where(playerManager.IsConnected).Select(player => player.HeroId).ToArray();

        bool changed = false;
        foreach (var menu in settlementMenuAccess.GetOpenMenus())
        {
            if (!connectedHeroes.Contains(menu.HeroId) || !CanUseSettlement(menu.HeroId, menu.SettlementId))
            {
                changed |= settlementMenuAccess.Release(menu.HeroId);
            }
        }
        return changed;
    }

    private bool CanUseSettlement(string heroId, string settlementId)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(heroId, out var hero)) return false;
        if (!objectManager.TryGetObjectWithLogging<Settlement>(settlementId, out var settlement)) return false;

        if (hero.CurrentSettlement != settlement) return false;

        return !hero.IsPrisoner && hero.Clan == settlement.OwnerClan;
    }

    private void Broadcast()
    {
        network.SendAll(new NetworkSettlementMenusChanged(settlementMenuAccess.GetOpenMenus()));
    }
}
