using Common.Messaging;
using Common.Network.Messages;
using Coop.Core.Client.Messages;
using Coop.Core.Common.Services.Connection.Messages;
using GameInterface.Services.GameState.Messages;
using Missions.Messages;
using System;
using System.Globalization;

namespace Coop.Core.Client.Services.Discord;

/// <summary>Tracks only this client's playable session, not remote players' battles.</summary>
internal sealed class DiscordPresenceHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IDiscordPresenceClient presenceClient;
    private readonly Func<DateTime> getUtcNow;
    private readonly object gate = new object();
    private DateTime? connectedAtUtc;
    private bool campaignReady;
    private string battleId;
    private int playerCount = 1;
    private string lastDetails;
    private string lastState;
    private bool disposed;

    public DiscordPresenceHandler(IMessageBroker messageBroker, IDiscordPresenceClient presenceClient)
        : this(messageBroker, presenceClient, () => DateTime.UtcNow)
    {
    }

    internal DiscordPresenceHandler(
        IMessageBroker messageBroker, IDiscordPresenceClient presenceClient, Func<DateTime> getUtcNow)
    {
        if (messageBroker == null) throw new ArgumentNullException(nameof(messageBroker));
        if (presenceClient == null) throw new ArgumentNullException(nameof(presenceClient));
        if (getUtcNow == null) throw new ArgumentNullException(nameof(getUtcNow));
        this.messageBroker = messageBroker;
        this.presenceClient = presenceClient;
        this.getUtcNow = getUtcNow;

        messageBroker.Subscribe<NetworkConnected>(Handle_Connected);
        messageBroker.Subscribe<ClientCampaignReady>(Handle_CampaignReady);
        messageBroker.Subscribe<NetworkConnectedPlayersChanged>(Handle_PlayerCount);
        messageBroker.Subscribe<BattleMissionReady>(Handle_BattleReady);
        messageBroker.Subscribe<BattleMissionEnded>(Handle_BattleEnded);
        messageBroker.Subscribe<NetworkDisconnected>(Handle_Disconnected);
        messageBroker.Subscribe<EndCoopMode>(Handle_EndCoopMode);
        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
    }

    private void Handle_Connected(MessagePayload<NetworkConnected> _)
    {
        lock (gate)
        {
            if (disposed || connectedAtUtc.HasValue) return;
            connectedAtUtc = getUtcNow();
        }
    }

    private void Handle_CampaignReady(MessagePayload<ClientCampaignReady> _)
    {
        lock (gate)
        {
            if (disposed || !connectedAtUtc.HasValue) return;
            campaignReady = true;
            PublishPresence();
        }
    }

    private void Handle_PlayerCount(MessagePayload<NetworkConnectedPlayersChanged> payload)
    {
        lock (gate)
        {
            if (disposed || !connectedAtUtc.HasValue) return;
            // The server counts client connections, including this client; never add a host player.
            playerCount = Math.Max(1, payload.What.ConnectedPlayers);
            PublishPresence();
        }
    }

    private void Handle_BattleReady(MessagePayload<BattleMissionReady> payload)
    {
        lock (gate)
        {
            if (disposed || !campaignReady) return;
            battleId = payload.What.MapEventId;
            PublishPresence();
        }
    }

    private void Handle_BattleEnded(MessagePayload<BattleMissionEnded> payload)
    {
        lock (gate)
        {
            if (disposed || battleId != payload.What.MapEventId) return;
            battleId = null;
            PublishPresence();
        }
    }

    private void PublishPresence()
    {
        if (!campaignReady || !connectedAtUtc.HasValue) return;
        string details = battleId == null ? "In a co-op campaign" : "Fighting a battle";
        string state = playerCount.ToString(CultureInfo.InvariantCulture) +
            (playerCount == 1 ? " player" : " players");
        if (details == lastDetails && state == lastState) return;
        presenceClient.SetPresence(details, state, connectedAtUtc.Value);
        lastDetails = details;
        lastState = state;
    }

    private void Handle_Disconnected(MessagePayload<NetworkDisconnected> _) => ClearSession();
    private void Handle_EndCoopMode(MessagePayload<EndCoopMode> _) => ClearSession();
    private void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> _) => ClearSession();

    private void ClearSession()
    {
        lock (gate)
        {
            if (disposed) return;
            if (lastState != "Main Menu") presenceClient.SetMainMenu();
            connectedAtUtc = null;
            campaignReady = false;
            battleId = null;
            playerCount = 1;
            lastDetails = null;
            lastState = "Main Menu";
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            ClearSession();
            disposed = true;
        }
        messageBroker.Unsubscribe<NetworkConnected>(Handle_Connected);
        messageBroker.Unsubscribe<ClientCampaignReady>(Handle_CampaignReady);
        messageBroker.Unsubscribe<NetworkConnectedPlayersChanged>(Handle_PlayerCount);
        messageBroker.Unsubscribe<BattleMissionReady>(Handle_BattleReady);
        messageBroker.Unsubscribe<BattleMissionEnded>(Handle_BattleEnded);
        messageBroker.Unsubscribe<NetworkDisconnected>(Handle_Disconnected);
        messageBroker.Unsubscribe<EndCoopMode>(Handle_EndCoopMode);
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
    }
}
