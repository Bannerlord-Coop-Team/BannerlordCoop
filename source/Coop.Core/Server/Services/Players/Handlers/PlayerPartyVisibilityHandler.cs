using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network.Messages;
using Common.Util;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Save.Messages;
using GameInterface.Services.Armies;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyVisuals.Interfaces;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Players.Messages;
using GameInterface.Services.SiegeEvents.Interfaces;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Core.Server.Services.Players.Handlers;
/// <summary>
/// Server-side: hides a disconnected player's party from the map and stops it being simulated, then
/// restores it once the peer has synchronized the campaign. Parties in a MapEvent remain active so reconnect
/// saves preserve their battle membership.
/// <see cref="MobileParty.IsActive"/> syncs through AutoSync. <see cref="IPartyVisualInterface"/>
/// replicates the separate lifetime of the rendered map figure.
/// </summary>
internal class PlayerPartyVisibilityHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerPartyVisibilityHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IPlayerManager playerManager;
    private readonly IConnectionCollection connectionCollection;
    private readonly IObjectManager objectManager;
    private readonly IPartyVisualInterface partyVisuals;
    private readonly ISiegeEventInterface siegeEventInterface;
    private readonly IArmyPartyRemover armyPartyRemover;
    private readonly Dictionary<MobileParty, (MapEvent MapEvent, string ControllerId)> deferredMapEventParking = new();

    public PlayerPartyVisibilityHandler(
        IMessageBroker messageBroker,
        IPlayerManager playerManager,
        IConnectionCollection connectionCollection,
        IObjectManager objectManager,
        IPartyVisualInterface partyVisuals,
        ISiegeEventInterface siegeEventInterface,
        IArmyPartyRemover armyPartyRemover)
    {
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
        this.connectionCollection = connectionCollection;
        this.objectManager = objectManager;
        this.partyVisuals = partyVisuals;
        this.siegeEventInterface = siegeEventInterface;
        this.armyPartyRemover = armyPartyRemover;

        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Subscribe<PlayerCampaignSynchronized>(Handle_PlayerCampaignSynchronized);
        messageBroker.Subscribe<PlayerHeirSelectionRequested>(Handle_PlayerHeirSelectionRequested);
        messageBroker.Subscribe<PlayerHeirSelectionCompleted>(Handle_PlayerHeirSelectionCompleted);
        messageBroker.Subscribe<PlayerPartyReleasedFromCaptivity>(Handle_PlayerPartyReleasedFromCaptivity);
        messageBroker.Subscribe<MapEventFinalized>(Handle_MapEventFinalized);
        messageBroker.Subscribe<SavedPlayerRegistrationsRestored>(Handle_SavedPlayerRegistrationsRestored);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Unsubscribe<PlayerCampaignSynchronized>(Handle_PlayerCampaignSynchronized);
        messageBroker.Unsubscribe<PlayerHeirSelectionRequested>(Handle_PlayerHeirSelectionRequested);
        messageBroker.Unsubscribe<PlayerHeirSelectionCompleted>(Handle_PlayerHeirSelectionCompleted);
        messageBroker.Unsubscribe<PlayerPartyReleasedFromCaptivity>(Handle_PlayerPartyReleasedFromCaptivity);
        messageBroker.Unsubscribe<MapEventFinalized>(Handle_MapEventFinalized);
        messageBroker.Unsubscribe<SavedPlayerRegistrationsRestored>(Handle_SavedPlayerRegistrationsRestored);
        deferredMapEventParking.Clear();
    }

    private void Handle_SavedPlayerRegistrationsRestored(
        MessagePayload<SavedPlayerRegistrationsRestored> payload)
    {
        if (ModInformation.IsClient) return;

        foreach (var player in playerManager.Players)
        {
            if (playerManager.IsConnected(player)) continue;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party))
                continue;

            ParkParty(player, party, "its saved player is offline");
        }
    }

    /// <summary> A peer dropped: park its party and remove its map figure unless it is in a MapEvent.
    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        if (ModInformation.IsClient) return;

        var peer = payload.What.PlayerId;

        if (!TryResolveParty(peer, out var player, out var party))
        {
            // Not every disconnect belongs to a player mid-campaign so a miss can be expected, not an error
            return;
        }

        // playerManager's peer link is only needed to resolve the party above, drop it now regardless
        // of what happens below, so a stale peer never resolves to the wrong party
        playerManager.ClearPeer(peer);
        messageBroker.Publish(this, new PlayerConnectionStateChanged());

        GameThread.RunSafe(() =>
        {
            ParkParty(player, party, $"peer {peer.Id} disconnected");
        });
    }

    private void ParkParty(Player player, MobileParty party, string reason)
    {
        var mapEvent = party.MapEvent;
        if (mapEvent != null)
        {
            deferredMapEventParking[party] = (mapEvent, player.ControllerId);
            messageBroker.Publish(this, new PlayerDisconnectedFromMapEvent(player.ControllerId, mapEvent));
            Logger.Information(
                "Keeping party {PartyId} active in MapEvent {MapEventId} because {Reason}",
                party.StringId,
                mapEvent.StringId,
                reason);
            return;
        }

        var wasActive = party.IsActive;
        ParkOutsideMapEvent(party, queueArmyLeave: false);

        if (!wasActive)
        {
            Logger.Debug("Party {PartyId} already parked because {Reason}", party.StringId, reason);
            return;
        }

        Logger.Information("Parked party {PartyId} because {Reason}", party.StringId, reason);
    }

    private void Handle_PlayerHeirSelectionRequested(MessagePayload<PlayerHeirSelectionRequested> payload)
    {
        var hero = payload.What.PlayerHero;
        if (hero == null || !hero.IsDead) return;

        if (!objectManager.TryGetIdWithLogging(hero, out var heroId)) return;

        var player = playerManager.Players.FirstOrDefault(candidate => candidate.HeroId == heroId);
        if (player == null)
        {
            Logger.Error("Could not find the registered player for dead hero {HeroId}", heroId);
            return;
        }

        if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return;

        ParkParty(player, party, $"player hero {heroId} died");
    }

    private void Handle_PlayerHeirSelectionCompleted(MessagePayload<PlayerHeirSelectionCompleted> payload)
    {
        var hero = payload.What.PlayerHero;
        if (hero == null || !hero.IsAlive || hero.IsPrisoner || hero.PartyBelongedToAsPrisoner != null) return;

        if (!objectManager.TryGetIdWithLogging(hero, out var heroId)) return;

        var player = playerManager.Players.FirstOrDefault(candidate => candidate.HeroId == heroId);
        if (player == null ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party) ||
            party.IsActive)
        {
            return;
        }

        ActivateParty(party);
        Logger.Information($"Activated party {party.StringId} after player {heroId} selected an heir");
    }

    /// <summary> A peer finished campaign synchronization, un-park its party and rebuild its map figure.
    private void Handle_PlayerCampaignSynchronized(MessagePayload<PlayerCampaignSynchronized> payload)
    {
        if (ModInformation.IsClient) return;

        var peer = payload.What.PlayerId;

        if (!playerManager.TryGetPlayer(peer, out var player) ||
            !playerManager.TryGetPeer(player.ControllerId, out var currentPeer) ||
            !ReferenceEquals(currentPeer, peer))
        {
            Logger.Error("Could not resolve party for peer {Peer} on campaign entry", peer.Id);
            return;
        }

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) return;

            // Player connected while their hero is already dead.
            // Instead of restoring party bring up heir selection menu.
            if (hero.IsDead)
            {
                messageBroker.Publish(this, new PlayerHeirSelectionRequested(hero));
                return;
            }

            deferredMapEventParking.Remove(party);
            if (party.MapEvent != null)
                messageBroker.Publish(this, new PlayerReconnectedToMapEvent(player.ControllerId, party.MapEvent));

            if (party.IsActive)
            {
                return; // fresh join, never parked, nothing to restore
            }

            // Retrieves the player and outs it to the Hero Object
            // Checks if the player is prisoner or if they belong there
            // If they do, the Debug message appears.
            if (hero.IsPrisoner || hero.PartyBelongedToAsPrisoner != null)
            {
                Logger.Debug("Keeping captive party {PartyId} parked for peer {Peer}",
                    party.StringId,
                    peer.Id);
                return;
            }

            ActivateParty(party);
            Logger.Information("Restored party {PartyId} for reconnected peer {Peer}", party.StringId, peer.Id);
            messageBroker.Publish(this, new PlayerPartyRestoredOnReconnect(party));
        });
    }

    private void Handle_PlayerPartyReleasedFromCaptivity(
        MessagePayload<PlayerPartyReleasedFromCaptivity> payload)
    {
        if (ModInformation.IsClient) return;

        var party = payload.What.PlayerParty;
        if (party == null) return;

        party.IsActive = false;
        party.IsVisible = false;
        partyVisuals.RemoveVisual(party);

        if (!TryGetPlayer(party, out var player) ||
            IsPlayerHeroDead(player) ||
            !playerManager.IsConnected(player) ||
            !playerManager.TryGetPeer(player.ControllerId, out var peer) ||
            !connectionCollection.HasCompletedCampaignSynchronization(peer))
        {
            Logger.Information(
                "Kept released party {PartyId} parked because its player is offline or synchronizing",
                party.StringId);
            return;
        }

        ActivateParty(party);
        Logger.Information("Restored released party {PartyId} for peer {Peer}", party.StringId, peer.Id);
    }

    private void ActivateParty(MobileParty party)
    {
        party.IsActive = true;
        partyVisuals.CreateVisual(party);
        party.IsVisible = true;
        party.IsInspected = true;
    }

    private void Handle_MapEventFinalized(MessagePayload<MapEventFinalized> payload)
    {
        if (ModInformation.IsClient) return;

        foreach (var party in deferredMapEventParking
            .Where(entry => ReferenceEquals(entry.Value.MapEvent, payload.What.MapEvent))
            .Select(entry => entry.Key)
            .ToArray())
        {
            if (party.MapEvent != null) continue;

            var controllerId = deferredMapEventParking[party].ControllerId;
            deferredMapEventParking.Remove(party);
            if (!playerManager.TryGetPlayer(controllerId, out var player) ||
                (playerManager.IsConnected(player) && !IsPlayerHeroDead(player)))
            {
                continue;
            }

            // Queued because the battle can finalize while vanilla is still looping over the leader's attached parties
            ParkOutsideMapEvent(party, queueArmyLeave: true);
            Logger.Information(
                "Parked party {PartyId} after its MapEvent ended while its player was disconnected or dead",
                party.StringId);
        }
    }

    private void LeaveSiegeBeforeParking(MobileParty party)
    {
        if (party.BesiegerCamp == null) return;

        Logger.Information(
            "Removing disconnected party {PartyId} from its siege camp before parking",
            party.StringId);

        // AI army members leave the siege and the army of their parked leader, otherwise they stay stuck to it; players keep their siege role
        var army = party.Army;
        if (army?.LeaderParty == party)
        {
            foreach (var member in army.Parties.Where(member => member != party && !playerManager.Contains(member)).ToArray())
            {
                // One member's broken state must not stop the leader from being parked
                try
                {
                    if (member.BesiegerCamp != null)
                        siegeEventInterface.BreakSiegeForPartyOnly(member);

                    armyPartyRemover.Remove(member, army);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Could not take army member {MemberId} out of parked leader {PartyId}'s siege and army",
                        member.StringId, party.StringId);
                }
            }
        }

        siegeEventInterface.BreakSiegeForPartyOnly(party);
    }

    // The parking steps for a party outside a MapEvent, the counterpart of ActivateParty
    private void ParkOutsideMapEvent(MobileParty party, bool queueArmyLeave)
    {
        LeaveSiegeBeforeParking(party);

        // A follower leaves its army, otherwise vanilla keeps copying the leader's battle side and siege camp onto it while it is attached
        var army = party.Army;
        if (army != null && army.LeaderParty != party)
        {
            Logger.Information("Removing parked party {PartyId} from the army of {LeaderId}", party.StringId, army.LeaderParty?.StringId);

            // A queued leave runs on a later frame, after the party is parked
            if (queueArmyLeave)
                GameThread.EnqueueSafe(() => armyPartyRemover.Remove(party, army));
            else
                armyPartyRemover.Remove(party, army);
        }

        party.IsActive = false;
        party.IsVisible = false;
        partyVisuals.RemoveVisual(party);
    }

    private bool TryResolveParty(NetPeer peer, out Player player, out MobileParty party)
    {
        player = null;
        party = null;

        return playerManager.TryGetPlayer(peer, out player) &&
            objectManager.TryGetObjectWithLogging(player.MobilePartyId, out party);
    }

    private bool TryGetPlayer(MobileParty party, out Player player)
    {
        player = null;
        if (!objectManager.TryGetIdWithLogging(party, out var partyId)) return false;

        player = playerManager.Players.FirstOrDefault(candidate => candidate.MobilePartyId == partyId);
        return player != null;
    }

    private bool IsPlayerHeroDead(Player player)
    {
        return objectManager.TryGetObject<Hero>(player.HeroId, out var hero) && hero.IsDead;
    }
}
