using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Handlers;

internal class ClanFinanceHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IClanFinance clanFinance;
    private readonly IPlayerManager playerManager;

    public ClanFinanceHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IClanFinance clanFinance,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.clanFinance = clanFinance;
        this.playerManager = playerManager;

        messageBroker.Subscribe<ClanFinanceChangeRequested>(Handle_ClanFinanceChangeRequested);
        messageBroker.Subscribe<RequestClanFinanceChange>(Handle_RequestClanFinanceChange);

        messageBroker.Subscribe<ClanFinanceChanged>(Handle_ClanFinanceChanged);
        messageBroker.Subscribe<NetworkClanFinanceChanged>(Handle_NetworkClanFinanceChanged);

        messageBroker.Subscribe<InitializeClientClanFinance>(Handle_InitializeClientClanFinance);
        messageBroker.Subscribe<PlayerConnectionStateChanged>(Handle_PlayerConnectionStateChanged);
        messageBroker.Subscribe<NetworkClanFinanceConnectionsChanged>(Handle_NetworkClanFinanceConnectionsChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ClanFinanceChangeRequested>(Handle_ClanFinanceChangeRequested);
        messageBroker.Unsubscribe<RequestClanFinanceChange>(Handle_RequestClanFinanceChange);

        messageBroker.Unsubscribe<ClanFinanceChanged>(Handle_ClanFinanceChanged);
        messageBroker.Unsubscribe<NetworkClanFinanceChanged>(Handle_NetworkClanFinanceChanged);

        messageBroker.Unsubscribe<InitializeClientClanFinance>(Handle_InitializeClientClanFinance);
        messageBroker.Unsubscribe<PlayerConnectionStateChanged>(Handle_PlayerConnectionStateChanged);
        messageBroker.Unsubscribe<NetworkClanFinanceConnectionsChanged>(Handle_NetworkClanFinanceConnectionsChanged);
    }

    private void Handle_ClanFinanceChangeRequested(MessagePayload<ClanFinanceChangeRequested> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.Actor, out var actorId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Member, out var memberId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Clan, out var clanId)) return;

        network.SendAll(new RequestClanFinanceChange(actorId, memberId, clanId, data.Amount, data.IsTribute));
    }

    private void Handle_RequestClanFinanceChange(MessagePayload<RequestClanFinanceChange> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.ActorId, out var actor)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MemberId, out var member)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(data.ClanId, out var clan)) return;

            clanFinance.TryChange(actor, member, clan, data.Amount, data.IsTribute);
        });
    }

    private void Handle_ClanFinanceChanged(MessagePayload<ClanFinanceChanged> payload)
    {
        network.SendAll(new NetworkClanFinanceChanged(payload.What.MemberId, payload.What.Settings));
    }

    private void Handle_NetworkClanFinanceChanged(MessagePayload<NetworkClanFinanceChanged> payload)
    {
        GameThread.RunSafe(() =>
        {
            clanFinance.Apply(payload.What.MemberId, payload.What.Settings);
        });
    }

    private void Handle_InitializeClientClanFinance(MessagePayload<InitializeClientClanFinance> payload)
    {
        GameThread.RunSafe(() =>
        {
            clanFinance.Initialize(payload.What.Settings);
        });
    }

    private void Handle_PlayerConnectionStateChanged(MessagePayload<PlayerConnectionStateChanged> payload)
    {
        GameThread.RunSafe(() =>
        {
            var players = playerManager.Players;
            var disconnectedHeroes = players.Where(player => !playerManager.IsConnected(player))
                .Select(player => player.HeroId).ToArray();

            // Send to clients to update their locally stored disconnected heroes
            network.SendAll(new NetworkClanFinanceConnectionsChanged(disconnectedHeroes));

            foreach (var player in players)
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) continue;

                // Send message to refresh clan finances for clients in the clan management screen
                messageBroker.Publish(this, new ClanManagementChanged(hero.Clan, ClanManagementRefresh.Finances));
            }
        });
    }

    private void Handle_NetworkClanFinanceConnectionsChanged(MessagePayload<NetworkClanFinanceConnectionsChanged> payload)
    {
        GameThread.RunSafe(() =>
        {
            // Update disconnected heroes on clients
            // This is needed to accurately estimate gold changes locally when clan members are offline
            clanFinance.UpdateDisconnectedHeroes(payload.What.DisconnectedHeroIds);
        });
    }
}
