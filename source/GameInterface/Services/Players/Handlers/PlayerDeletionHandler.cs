using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Actions.Patches;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Messages;
using GameInterface.Services.SiegeEvents.Interfaces;
using LiteNetLib;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.Players.Handlers;

/// <summary>
/// Deletes the requesting player and its world objects, optionally keeping the peer connected
/// until the death statistics are dismissed.
/// </summary>
internal class PlayerDeletionHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerDeletionHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ISiegeEventInterface siegeEventInterface;

    public PlayerDeletionHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ISiegeEventInterface siegeEventInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.siegeEventInterface = siegeEventInterface;

        messageBroker.Subscribe<PlayerDeleteRequested>(Handle_PlayerDeleteRequested);
        messageBroker.Subscribe<NetworkRequestDeletePlayer>(Handle_NetworkRequestDeletePlayer);
        messageBroker.Subscribe<NetworkPlayerRemoved>(Handle_NetworkPlayerRemoved);
        messageBroker.Subscribe<NetworkDeletePlayerDenied>(Handle_NetworkDeletePlayerDenied);
        messageBroker.Subscribe<PlayerDisconnectRequested>(Handle_PlayerDisconnectRequested);
        messageBroker.Subscribe<NetworkRequestPlayerDisconnect>(Handle_NetworkRequestPlayerDisconnect);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerDeleteRequested>(Handle_PlayerDeleteRequested);
        messageBroker.Unsubscribe<NetworkRequestDeletePlayer>(Handle_NetworkRequestDeletePlayer);
        messageBroker.Unsubscribe<NetworkPlayerRemoved>(Handle_NetworkPlayerRemoved);
        messageBroker.Unsubscribe<NetworkDeletePlayerDenied>(Handle_NetworkDeletePlayerDenied);
        messageBroker.Unsubscribe<PlayerDisconnectRequested>(Handle_PlayerDisconnectRequested);
        messageBroker.Unsubscribe<NetworkRequestPlayerDisconnect>(Handle_NetworkRequestPlayerDisconnect);
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

        network.SendAll(new NetworkRequestDeletePlayer(heroId, payload.What.KeepConnected));
    }

    private void Handle_PlayerDisconnectRequested(MessagePayload<PlayerDisconnectRequested> obj)
    {
        if (ModInformation.IsServer) return;

        network.SendAll(new NetworkRequestPlayerDisconnect());
    }

    private void Handle_NetworkRequestPlayerDisconnect(MessagePayload<NetworkRequestPlayerDisconnect> obj)
    {
        if (ModInformation.IsClient) return;
        if (obj.Who is not NetPeer peer) return;

        GameThread.RunSafe(() =>
        {
            peer.Disconnect();
        });
    }

    /// <summary>
    /// Server: delete the requesting peer's player, optionally keeping its connection.
    /// </summary>
    private void Handle_NetworkRequestDeletePlayer(MessagePayload<NetworkRequestDeletePlayer> payload)
    {
        if (ModInformation.IsClient) return;

        if (!(payload.Who is NetPeer peer))
        {
            Logger.Error("{Message} arrived without a source peer; cannot resolve the requesting player",
                nameof(NetworkRequestDeletePlayer));
            return;
        }

        var data = payload.What;
        GameThread.RunSafe(() => DeletePlayer(peer, data.HeroId, data.KeepConnected), context: nameof(PlayerDeletionHandler));
    }

    private void DeletePlayer(NetPeer peer, string requestedHeroId, bool keepConnected)
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

        bool hasDied = hero != null && (!hero.IsAlive || hero.DeathMark != KillCharacterAction.KillCharacterActionDetail.None);
        if (!hasDied && party != null && (party.Party?.MapEvent != null || party.BesiegerCamp != null))
        {
            network.Send(peer, new NetworkDeletePlayerDenied(
                "Cannot delete a player whose party is in a battle or siege; leave it first " +
                "(coop.debug.mobileparty.unstuck can force the exit)."));
            return;
        }

        Logger.Information("Deleting player {ControllerId} (hero {HeroId}) at its own request",
            player.ControllerId, player.HeroId);

        messageBroker.Publish(this, new PlayerDeletionStarted(peer));

        if (hasDied && party != null)
        {
            if (party.Party.MapEvent != null)
            {
                messageBroker.Publish(this, new PlayerLeaveBattleAttempted(party.Party, finishLocalMenus: false));
            }

            if (party.BesiegerCamp != null)
            {
                siegeEventInterface.BreakSiege(party);
            }
        }

        if (party?.CurrentSettlement != null)
        {
            TryStep("party settlement exit", () => LeaveSettlementAction.ApplyForParty(party));
        }

        playerManager.RemovePlayer(player);

        network.SendAllBut(peer, new NetworkPlayerRemoved(player.ControllerId, player.HeroId));

        // Only the no-heir game-over request keeps its peer connected for the statistics screen
        if (keepConnected && hasDied && hero?.Clan != null)
        {
            TryStep("player game over clan cleanup", () => CleanupPlayerClanAfterGameOver(hero));
        }

        if (!keepConnected) peer.Disconnect();

        // Keep patches live and preserve the real death cause and killer.
        if (hero != null && hero.IsAlive)
        {
            TryStep("hero kill", () =>
            {
                if (hero.DeathMark != KillCharacterAction.KillCharacterActionDetail.None)
                {
                    KillCharacterAction.ApplyByDeathMarkForced(hero, false);
                }
                else
                {
                    KillCharacterAction.ApplyByRemove(hero, false, true);
                }
            });
        }

        // Re-resolved because the kill can cascade into destroying the party itself. Inactive
        // parties are destroyed too: captivity parks the registered player party deactivated,
        // and it must not outlive the deleted player.
        if (objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out MobileParty remainingParty))
        {
            TryStep("party destroy", () => DestroyPartyAction.Apply(null, remainingParty));
        }
    }

    private static void CleanupPlayerClanAfterGameOver(Hero playerHero)
    {
        Clan playerClan = playerHero.Clan;

        // Player registration has already been removed, so apply the same ruler succession used
        // when an AI kingdom leader dies before considering whether the clan itself should end
        if (playerClan.Leader == playerHero)
        {
            KillCharacterActionPatches.HandleKingdomLeaderDeath(playerHero);
        }

        if (playerClan.IsEliminated ||
            playerClan.IsBanditFaction ||
            playerClan.GetHeirApparents().Count > 0) return;

        if (playerClan.Leader == playerHero)
        {
            DestroyClanAction.ApplyByClanLeaderDeath(playerClan);
        }
        else
        {
            DestroyClanAction.Apply(playerClan);
        }
    }

    /// <summary>
    /// Client: the server deleted a player — drop it from the local registry so the follow-up
    /// kill/destroy replication applies, and run the native death transition for its hero. The
    /// hero state itself also arrives through the hero field sync, but that wire application
    /// only assigns the field; ChangeState here keeps the clan alive-lord caches and
    /// CampaignObjectManager's alive/dead lists correct too.
    /// </summary>
    private void Handle_NetworkPlayerRemoved(MessagePayload<NetworkPlayerRemoved> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        // Blocking: a client still loading in can replay [player created, player removed, player
        // created again] back to back, and RemotePlayerHeroHandler's duplicate check reads the
        // registry on this thread — the removal must be applied before the next message.
        GameThread.RunSafe(() =>
        {
            if (playerManager.TryGetPlayer(data.ControllerId, out var player))
            {
                playerManager.RemovePlayer(player);
                Logger.Information("Player {ControllerId} (hero {HeroId}) was deleted by the server",
                    data.ControllerId, data.HeroId);
            }
            else
            {
                Logger.Debug("Deleted player {ControllerId} was not registered on this client", data.ControllerId);
            }

            if (objectManager.TryGetObject<Hero>(data.HeroId, out var hero) && hero.IsAlive)
            {
                using (new AllowedThread())
                {
                    hero.ChangeState(Hero.CharacterStates.Dead);
                }
            }
        }, blocking: true, context: nameof(PlayerDeletionHandler));
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
