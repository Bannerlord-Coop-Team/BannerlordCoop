using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Messages;
using LiteNetLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.Players.Handlers;

/// <summary>
/// Handles the coop.delete_player flow. The client forwards <see cref="PlayerDeleteRequested"/> to
/// the server as <see cref="NetworkRequestDeletePlayer"/>; the server resolves the player from the
/// requesting connection (never from client-sent ids), deletes its registration everywhere
/// (<see cref="NetworkPlayerRemoved"/> lifts the player-party destroy protection on the other
/// clients first), kicks the requester, then kills the hero and destroys the party with patches
/// live so the existing death/destroy sync replicates both to the remaining clients. A request
/// that cannot be applied is answered with <see cref="NetworkDeletePlayerDenied"/> and the
/// requester stays connected.
/// </summary>
internal class PlayerDeletionHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerDeletionHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;

    public PlayerDeletionHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;

        messageBroker.Subscribe<PlayerDeleteRequested>(Handle_PlayerDeleteRequested);
        messageBroker.Subscribe<NetworkRequestDeletePlayer>(Handle_NetworkRequestDeletePlayer);
        messageBroker.Subscribe<NetworkPlayerRemoved>(Handle_NetworkPlayerRemoved);
        messageBroker.Subscribe<NetworkDeletePlayerDenied>(Handle_NetworkDeletePlayerDenied);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerDeleteRequested>(Handle_PlayerDeleteRequested);
        messageBroker.Unsubscribe<NetworkRequestDeletePlayer>(Handle_NetworkRequestDeletePlayer);
        messageBroker.Unsubscribe<NetworkPlayerRemoved>(Handle_NetworkPlayerRemoved);
        messageBroker.Unsubscribe<NetworkDeletePlayerDenied>(Handle_NetworkDeletePlayerDenied);
    }

    /// <summary>
    /// Client: forward the local delete request to the server.
    /// </summary>
    private void Handle_PlayerDeleteRequested(MessagePayload<PlayerDeleteRequested> payload)
    {
        if (ModInformation.IsServer) return;

        // Advisory only, for server-side cross-checking; the server derives the player to delete
        // from the requesting connection.
        objectManager.TryGetId(Hero.MainHero, out var heroId);

        network.SendAll(new NetworkRequestDeletePlayer(heroId));
    }

    /// <summary>
    /// Server: delete the requesting peer's player and disconnect it.
    /// </summary>
    private void Handle_NetworkRequestDeletePlayer(MessagePayload<NetworkRequestDeletePlayer> payload)
    {
        if (!ModInformation.IsServer) return;

        if (!(payload.Who is NetPeer peer))
        {
            Logger.Error("{Message} arrived without a source peer; cannot resolve the requesting player",
                nameof(NetworkRequestDeletePlayer));
            return;
        }

        var requestedHeroId = payload.What.HeroId;
        GameThread.RunSafe(() => DeletePlayer(peer, requestedHeroId), context: nameof(PlayerDeletionHandler));
    }

    private void DeletePlayer(NetPeer peer, string requestedHeroId)
    {
        if (!playerManager.TryGetPlayer(peer, out var player))
        {
            // Never kick here: a peer without a player may be mid-join, and its request is at
            // worst a no-op.
            Logger.Warning("Delete request from peer {PeerId} with no registered player", peer.Id);
            network.Send(peer, new NetworkDeletePlayerDenied("No registered player found for this connection."));
            return;
        }

        if (!string.IsNullOrEmpty(requestedHeroId) && requestedHeroId != player.HeroId)
        {
            Logger.Warning(
                "Delete request names hero {RequestedHeroId} but the connection's registered hero is {HeroId}; deleting the registered hero",
                requestedHeroId, player.HeroId);
        }

        // Either may be gone already (e.g. the hero died earlier); the deletion still removes the
        // registration and whatever objects remain.
        objectManager.TryGetObject<Hero>(player.HeroId, out var hero);
        objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party);

        if (party != null && (party.Party?.MapEvent != null || party.BesiegerCamp != null))
        {
            network.Send(peer, new NetworkDeletePlayerDenied(
                "Cannot delete a player whose party is in a battle or siege; leave it first " +
                "(coop.debug.mobileparty.unstuck can force the exit)."));
            return;
        }

        Logger.Information("Deleting player {ControllerId} (hero {HeroId}) at its own request",
            player.ControllerId, player.HeroId);

        playerManager.RemovePlayer(player);

        // The other clients drop their player registration off this message. It must go out
        // BEFORE the kill/destroy below replicate: DestroyPartyActionPatch blocks destroys of
        // registered player parties on every peer, and all these messages ride the same ordered
        // stream.
        network.SendAllBut(peer, new NetworkPlayerRemoved(player.ControllerId, player.HeroId));

        // Kick the requester before applying the world changes so the resulting broadcasts skip
        // it — that client is heading to the main menu and must not apply a destroy of its own
        // main party. Disconnect marks the peer non-connected synchronously, so later SendAlls
        // no longer include it.
        peer.Disconnect();

        // Patches stay live for both actions so the server-side apply is what replicates: the
        // kill is server-only and its state flip broadcasts through the hero field sync (clan
        // cascades via their own patched actions), and the party destroy's prefix broadcasts the
        // destruction to the remaining clients. Kill first so the hero leaves the party through
        // the native death flow, then destroy whatever party is left, mirroring vanilla's defeat
        // teardown order.
        if (hero != null && hero.IsAlive)
        {
            // (showNotification: false, isForced: true) — forced skips the can-die model checks,
            // same as the companion removal flow.
            TryStep("hero kill", () => KillCharacterAction.ApplyByRemove(hero, false, true));
        }

        if (party != null && party.IsActive)
        {
            TryStep("party destroy", () => DestroyPartyAction.Apply(null, party));
        }
    }

    /// <summary>
    /// Client: the server deleted a player — drop it from the local registry so the follow-up
    /// kill/destroy replication applies. The world-side changes themselves arrive through the
    /// normal sync flows (the hero state via the hero field sync, the party via the destroy
    /// broadcast); this message only carries the session-level registration, which no game-state
    /// sync can.
    /// </summary>
    private void Handle_NetworkPlayerRemoved(MessagePayload<NetworkPlayerRemoved> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!playerManager.TryGetPlayer(data.ControllerId, out var player))
            {
                Logger.Debug("Deleted player {ControllerId} was not registered on this client", data.ControllerId);
                return;
            }

            playerManager.RemovePlayer(player);
            Logger.Information("Player {ControllerId} (hero {HeroId}) was deleted by the server",
                data.ControllerId, data.HeroId);
        }, context: nameof(PlayerDeletionHandler));
    }

    /// <summary>
    /// Requesting client: the server denied the delete; surface the reason.
    /// </summary>
    private void Handle_NetworkDeletePlayerDenied(MessagePayload<NetworkDeletePlayerDenied> payload)
    {
        if (ModInformation.IsServer) return;

        var reason = payload.What.Reason ?? "The server denied the delete request.";

        // Network handlers run on the poller thread; the display is UI work.
        GameThread.RunSafe(() =>
        {
            messageBroker.Publish(this, new PlayerDeleteDenied(reason));
            ShowMessage($"[DeletePlayer] {reason}");
        }, context: nameof(PlayerDeletionHandler));
    }

    private static void TryStep(string step, Action apply)
    {
        try
        {
            apply();
        }
        catch (Exception e)
        {
            // The registration is already gone and the peer kicked; a failed world-side step must
            // not abort the rest of the teardown.
            Logger.Error(e, "Delete player step {Step} failed", step);
        }
    }

    private static void ShowMessage(string text)
    {
        try
        {
            InformationManager.DisplayMessage(new InformationMessage(text));
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to display the delete player message");
        }
    }
}
