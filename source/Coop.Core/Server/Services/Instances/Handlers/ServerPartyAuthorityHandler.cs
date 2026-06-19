using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Coop.Core.Server.Services.Instances.Messages;
using GameInterface.Missions;
using GameInterface.Missions.Services.Network.Messages;
using LiteNetLib;
using System.Collections.Concurrent;
using System.Linq;

namespace Coop.Core.Server.Services.Instances.Handlers;

/// <summary>
/// Server-side party control-authority arbiter. Records every spawned party authoritatively, hands a
/// disconnected client's parties to the host (<see cref="CoopServer.ServerControllerId"/>), and hands them
/// back when the same controller rejoins — broadcasting each change as <see cref="PartyControlChanged"/> so
/// clients mirror it. The host is the sole decider of authority; clients only apply.
/// </summary>
public class ServerPartyAuthorityHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IMissionPartyRegistry partyRegistry;

    // Which connection announced each owner, so an ungraceful disconnect resolves to its parties without
    // depending on the membership routing table (which is consumed elsewhere).
    private readonly ConcurrentDictionary<NetPeer, string> peerToOwner = new();

    public ServerPartyAuthorityHandler(IMessageBroker messageBroker, INetwork network, IMissionPartyRegistry partyRegistry)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.partyRegistry = partyRegistry;

        messageBroker.Subscribe<PartySpawned>(Handle_PartySpawned);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Subscribe<MissionEntered>(Handle_MissionEntered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartySpawned>(Handle_PartySpawned);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Unsubscribe<MissionEntered>(Handle_MissionEntered);
    }

    private void Handle_PartySpawned(MessagePayload<PartySpawned> payload)
    {
        var message = payload.What;

        // Record authoritatively, then relay so every client registers the same party.
        if (partyRegistry.TryGetParty(message.PartyId, out _) == false)
            partyRegistry.TryRegisterParty(message.PartyId, message.OriginalOwner, message.LeaderAgentId, message.TroopAgentIds, out _);

        // Map the announcing connection to its owner (client-spawned parties only; the host's NPC parties
        // are not announced from a peer, so they never transfer on a disconnect).
        if (payload.Who is NetPeer peer)
            peerToOwner[peer] = message.OriginalOwner;

        network.SendAll(message);
    }

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        if (peerToOwner.TryRemove(payload.What.PlayerId, out var ownerId) == false) return;

        // Host assumes control of every party the departed owner was still driving.
        foreach (var party in partyRegistry.GetPartiesOwnedBy(ownerId))
        {
            if (party.CurrentAuthority != ownerId) continue;

            partyRegistry.TryTransferAuthority(party.PartyId, CoopServer.ServerControllerId);
            network.SendAll(new PartyControlChanged(party.PartyId, CoopServer.ServerControllerId));
        }
    }

    private void Handle_MissionEntered(MessagePayload<MissionEntered> payload)
    {
        var peer = (NetPeer)payload.Who;
        var ownerId = payload.What.ControllerId;

        // The rejoin arrives on a fresh connection — re-map it so a later disconnect still resolves.
        peerToOwner[peer] = ownerId;

        // Hand back every party the host is holding on this owner's behalf.
        foreach (var party in partyRegistry.GetPartiesOwnedBy(ownerId))
        {
            if (party.CurrentAuthority != CoopServer.ServerControllerId) continue;

            // Resync the rejoining client before returning control. The host owns the live agent state;
            // here only the ids are known, so richer fields are filled by the host-side capture.
            var snapshot = new PartyStateSnapshot(
                party.PartyId,
                party.AllAgentIds.Select(id => new AgentSnapshot(id, 0f, true)).ToArray());
            network.Send(peer, snapshot);

            partyRegistry.TryTransferAuthority(party.PartyId, ownerId);
            network.SendAll(new PartyControlChanged(party.PartyId, ownerId));
        }
    }
}
