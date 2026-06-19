using Common.Messaging;
using GameInterface.Missions;
using GameInterface.Missions.Services.Network.Messages;

namespace Coop.Core.Client.Missions.Handlers;

/// <summary>
/// Client-side party control-authority mirror. Registers parties the host announces
/// (<see cref="PartySpawned"/>) and applies host-decided transfers (<see cref="PartyControlChanged"/>) to the
/// local <see cref="IMissionPartyRegistry"/>, so each client's authority view matches the host's.
/// </summary>
public class ClientPartyAuthorityHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IMissionPartyRegistry partyRegistry;

    public ClientPartyAuthorityHandler(IMessageBroker messageBroker, IMissionPartyRegistry partyRegistry)
    {
        this.messageBroker = messageBroker;
        this.partyRegistry = partyRegistry;

        messageBroker.Subscribe<PartySpawned>(Handle_PartySpawned);
        messageBroker.Subscribe<PartyControlChanged>(Handle_PartyControlChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartySpawned>(Handle_PartySpawned);
        messageBroker.Unsubscribe<PartyControlChanged>(Handle_PartyControlChanged);
    }

    private void Handle_PartySpawned(MessagePayload<PartySpawned> payload)
    {
        var message = payload.What;
        if (partyRegistry.TryGetParty(message.PartyId, out _)) return;

        partyRegistry.TryRegisterParty(message.PartyId, message.OriginalOwner, message.LeaderAgentId, message.TroopAgentIds, out _);
    }

    private void Handle_PartyControlChanged(MessagePayload<PartyControlChanged> payload)
    {
        var message = payload.What;
        partyRegistry.TryTransferAuthority(message.PartyId, message.NewAuthority);
    }
}
