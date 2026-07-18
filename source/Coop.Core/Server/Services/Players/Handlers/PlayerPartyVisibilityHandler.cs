using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.Util;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using GameInterface.Services.PartyVisuals.Extensions;
using GameInterface.Services.PartyVisuals.Messages;
using GameInterface.Services.Players;
using HarmonyLib;
using LiteNetLib;
using SandBox.View.Map.Managers;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Core.Server.Services.Players.Handlers;
/// <summary>
/// Server-side: hides a disconnected player's party from the map and stops it being simulated, then
/// restores it once the peer is back in the campaign. Parties in a MapEvent remain active so reconnect
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
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly Dictionary<MobileParty, MapEvent> deferredMapEventParking = new();

    public PlayerPartyVisibilityHandler(
        IMessageBroker messageBroker,
        IPlayerManager playerManager,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Subscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
        messageBroker.Subscribe<MapEventFinalized>(Handle_MapEventFinalized);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Unsubscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
        messageBroker.Unsubscribe<MapEventFinalized>(Handle_MapEventFinalized);
        deferredMapEventParking.Clear();
    }

    /// <summary> A peer dropped: park its party and remove its map figure unless it is in a MapEvent.
    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        if (ModInformation.IsClient) return;

        var peer = payload.What.PlayerId;

        if (!TryResolveParty(peer, out var party))
        {
            // Not every disconnect belongs to a player mid-campaign so a miss can be expected, not an error
            return;
        }

        // playerManager's peer link is only needed to resolve the party above, drop it now regardless
        // of what happens below, so a stale peer never resolves to the wrong party
        playerManager.ClearPeer(peer);

        GameThread.RunSafe(() =>
        {
            var mapEvent = party.MapEvent;
            if (mapEvent != null)
            {
                deferredMapEventParking[party] = mapEvent;
                Logger.Information(
                    "Keeping party {PartyId} active in MapEvent {MapEventId} after peer {Peer} disconnected",
                    party.StringId,
                    mapEvent.StringId,
                    peer.Id);
                return;
            }

            if (!party.IsActive)
            {
                Logger.Debug("Party {PartyId} already parked, skipping", party.StringId);
                return;
            }

            party.IsActive = false;

            RemoveVisual(party);

            Logger.Information("Parked party {PartyId} for disconnected peer {Peer}", party.StringId, peer.Id);
        });
    }

    /// <summary> A peer (re)entered the campaign, un-park its party and rebuild its map figure.
    private void Handle_PlayerCampaignEntered(MessagePayload<PlayerCampaignEntered> payload)
    {
        if (ModInformation.IsClient) return;

        var peer = payload.What.playerId;

        if (!playerManager.TryGetPlayer(peer, out var player) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party))
        {
            Logger.Error("Could not resolve party for peer {Peer} on campaign entry", peer.Id);
            return;
        }

        GameThread.RunSafe(() =>
        {
            deferredMapEventParking.Remove(party);
            if (party.MapEvent != null)
                messageBroker.Publish(this, new PlayerReconnectedToMapEvent());

            if (party.IsActive)
            {
                return; // fresh join, never parked, nothing to restore
            }

            party.IsActive = true;
            CreateVisual(party, player.MobilePartyId);
            party.Party.UpdateVisibilityAndInspected(party.Position);
            Logger.Information("Restored party {PartyId} for reconnected peer {Peer}", party.StringId, peer.Id);
        });
    }

    private void Handle_MapEventFinalized(MessagePayload<MapEventFinalized> payload)
    {
        if (ModInformation.IsClient) return;

        foreach (var party in deferredMapEventParking
            .Where(entry => ReferenceEquals(entry.Value, payload.What.MapEvent))
            .Select(entry => entry.Key)
            .ToArray())
        {
            if (party.MapEvent != null) continue;

            deferredMapEventParking.Remove(party);
            if (!party.IsActive || !IsDisconnectedPlayerParty(party)) continue;

            party.IsActive = false;
            RemoveVisual(party);
            Logger.Information(
                "Parked party {PartyId} after its MapEvent ended while its player was disconnected",
                party.StringId);
        }
    }

    private bool IsDisconnectedPlayerParty(MobileParty party) =>
        playerManager.Players.Any(player =>
            !playerManager.IsConnected(player) &&
            objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty) &&
            ReferenceEquals(playerParty, party));

    /// <summary>
    /// Removes the party's map figure and tells every client to do the same, mirroring
    /// PartyVisualLifetimeHandler's NetworkDestroyPartyVisual path exactly, but triggered by us
    /// rather than a native OnPartyRemoved call (the party itself is not being destroyed).
    /// </summary>
    private void RemoveVisual(MobileParty party)
    {
        var partyVisual = party.Party.GetPartyVisual();
        if (partyVisual == null) return;
        if (!objectManager.TryGetIdWithLogging(partyVisual, out string visualId))
            return;
        objectManager.Remove(partyVisual);

        using (new AllowedThread())
        {
            AccessTools.Method(typeof(MobilePartyVisualManager), "RemovePartyVisualForParty").Invoke(MobilePartyVisualManager.Current, new object[] { party });
        }

        network.SendAll(new NetworkDestroyPartyVisual(visualId));
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

    private bool TryResolveParty(NetPeer peer, out MobileParty party)
    {
        party = null;

        return playerManager.TryGetPlayer(peer, out var player) &&
            objectManager.TryGetObjectWithLogging(player.MobilePartyId, out party);
    }
}
