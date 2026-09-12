using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.Util;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Save.Messages;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using GameInterface.Services.PartyVisuals.Extensions;
using GameInterface.Services.PartyVisuals.Messages;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents.Interfaces;
using HarmonyLib;
using LiteNetLib;
using SandBox.View.Map.Managers;
using Serilog;
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
/// <see cref="MobileParty.IsActive"/> gates spotting/interaction/ticking (see
/// PartyVisibilityServerPatches, MobilePartyVisualManagerPatches) and is an AutoSync property, so
/// changing it syncs automatically. But it does NOT remove the party's rendered map figure.
/// The actual map figure is a <see cref="MobilePartyVisual"/>, created/destroyed through
/// MobilePartyVisualManager and replicated via the existing NetworkCreatePartyVisual /
/// NetworkDestroyPartyVisual (see PartyVisualLifetimeHandler)
/// </summary>
internal class PlayerPartyVisibilityHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerPartyVisibilityHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IPlayerManager playerManager;
    private readonly IConnectionCollection connectionCollection;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISiegeEventInterface siegeEventInterface;
    private readonly Dictionary<MobileParty, (MapEvent MapEvent, string ControllerId)> deferredMapEventParking = new();

    public PlayerPartyVisibilityHandler(
        IMessageBroker messageBroker,
        IPlayerManager playerManager,
        IConnectionCollection connectionCollection,
        IObjectManager objectManager,
        INetwork network,
        ISiegeEventInterface siegeEventInterface)
    {
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
        this.connectionCollection = connectionCollection;
        this.objectManager = objectManager;
        this.network = network;
        this.siegeEventInterface = siegeEventInterface;

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

        LeaveSiegeBeforeParking(party);

        var wasActive = party.IsActive;
        party.IsActive = false;
        party.IsVisible = false;
        RemoveVisual(party);

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

        ActivateParty(party, player.MobilePartyId);
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
                messageBroker.Publish(this, new PlayerReconnectedToMapEvent());

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

            ActivateParty(party, player.MobilePartyId);
            Logger.Information("Restored party {PartyId} for reconnected peer {Peer}", party.StringId, peer.Id);
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
        RemoveVisual(party);

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

        ActivateParty(party, player.MobilePartyId);
        Logger.Information("Restored released party {PartyId} for peer {Peer}", party.StringId, peer.Id);
    }

    private void ActivateParty(MobileParty party, string mobilePartyId)
    {
        party.IsActive = true;
        CreateVisual(party, mobilePartyId);
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

            LeaveSiegeBeforeParking(party);
            party.IsActive = false;
            party.IsVisible = false;
            RemoveVisual(party);
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
        siegeEventInterface.BreakSiegeForPartyOnly(party);
    }

    /// <summary>
    /// Removes the party's map figure and tells every client to do the same, mirroring
    /// PartyVisualLifetimeHandler's NetworkDestroyPartyVisual path exactly, but triggered by us
    /// rather than a native OnPartyRemoved call (the party itself is not being destroyed).
    /// </summary>
    private void RemoveVisual(MobileParty party)
    {
        var partyVisual = party.Party.GetPartyVisual();
        if (partyVisual == null) return;
        if (!objectManager.TryGetIdWithLogging(partyVisual, out string partyVisualId))
            return;
        if (!objectManager.TryGetIdWithLogging(party, out string mobilePartyId))
            return;
        objectManager.Remove(partyVisual);

        using (new AllowedThread())
        {
            AccessTools.Method(typeof(MobilePartyVisualManager), "RemovePartyVisualForParty").Invoke(MobilePartyVisualManager.Current, new object[] { party });
        }

        network.SendAll(new NetworkDestroyPartyVisual(partyVisualId, mobilePartyId));
    }

    /// <summary>
    /// Recreates the party's map figure and tells every client to do the same, mirroring
    /// PartyVisualLifetimeHandler's NetworkCreatePartyVisual path exactly, but triggered by us
    /// rather than a native visual construction call.
    /// </summary>
    private void CreateVisual(MobileParty party, string mobilePartyId)
    {
        using (new AllowedThread())
        {
            party.CreateNewPartyVisual();
        }

        var partyVisual = party.Party.GetPartyVisual();
        if (partyVisual == null)
        {
            Logger.Error("CreateNewPartyVisual did not produce a visual for party {PartyId}", party.StringId);
            return;
        }

        if (!objectManager.AddNewObject(partyVisual, out var visualId))
        {
            Logger.Error("Failed to register recreated visual for party {PartyId}", party.StringId);
            return;
        }

        network.SendAll(new NetworkCreatePartyVisual(visualId, mobilePartyId));
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
