using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobileParties.Patches;

namespace Coop.Core.Client.Services.MobileParties.Handlers;

/// <summary>
/// Handles changes to parties for settlement entry and exit on the client side.
/// </summary>
public class ClientSettlementExitEnterHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ClientSettlementExitEnterHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<StartSettlementEncounterAttempted>(Handle);
        messageBroker.Subscribe<EndSettlementEncounterAttempted>(Handle);
        messageBroker.Subscribe<NetworkEndSettlementEncounter>(Handle);
        messageBroker.Subscribe<NetworkStartSettlementEncounter>(Handle);

        // Party encounter flow: forward the local attempt to the server, then apply the server's approval.
        messageBroker.Subscribe<StartPartyEncounterAttempted>(Handle);
        messageBroker.Subscribe<NetworkStartPartyEncounter>(Handle);

        messageBroker.Subscribe<NetworkPartyEnterSettlement>(Handle);
        messageBroker.Subscribe<NetworkPartyLeaveSettlement>(Handle);
    }



    public void Dispose()
    {
        messageBroker.Unsubscribe<StartSettlementEncounterAttempted>(Handle);
        messageBroker.Unsubscribe<EndSettlementEncounterAttempted>(Handle);
        messageBroker.Unsubscribe<NetworkEndSettlementEncounter>(Handle);
        messageBroker.Unsubscribe<NetworkStartSettlementEncounter>(Handle);

        // Party encounter flow cleanup.
        messageBroker.Unsubscribe<StartPartyEncounterAttempted>(Handle);
        messageBroker.Unsubscribe<NetworkStartPartyEncounter>(Handle);

        messageBroker.Unsubscribe<NetworkPartyEnterSettlement>(Handle);
        messageBroker.Unsubscribe<NetworkPartyLeaveSettlement>(Handle);
    }


    private void Handle(MessagePayload<StartSettlementEncounterAttempted> obj)
    {
        var payload = obj.What;
        var message = new NetworkRequestStartSettlementEncounter(payload.PartyId, payload.SettlementId);

        network.SendAll(message);
    }

    private void Handle(MessagePayload<EndSettlementEncounterAttempted> obj)
    {
        var payload = obj.What;
        var message = new NetworkRequestEndSettlementEncounter(payload.PartyId);

        network.SendAll(message);
    }

    private void Handle(MessagePayload<NetworkStartSettlementEncounter> obj)
    {
        var payload = obj.What;
        var message = new StartSettlementEncounter(payload.PartyId, payload.SettlementId);

        messageBroker.Publish(this, message);
    }

    private void Handle(MessagePayload<NetworkEndSettlementEncounter> obj)
    {
        var message = new EndSettlementEncounter();

        messageBroker.Publish(this, message);
    }

    private void Handle(MessagePayload<NetworkPartyEnterSettlement> obj)
    {
        var payload = obj.What;

        var message = new PartyEnterSettlement(payload.SettlementId, payload.PartyId);
        messageBroker.Publish(this, message);
    }

    private void Handle(MessagePayload<NetworkPartyLeaveSettlement> obj)
    {
        var payload = obj.What;
        var message = new PartyLeaveSettlement(payload.PartyId);

        messageBroker.Publish(this, message);
    }

    // Without this handler the client had no way to tell the server to start a party encounter,
    // so walking up to an NPC showed "Tried to start encounter" in the log but nothing happened.
    private void Handle(MessagePayload<StartPartyEncounterAttempted> obj)
    {
        var payload = obj.What;
        var message = new NetworkRequestStartPartyEncounter(payload.AttackerPartyId, payload.DefenderPartyId);

        network.SendAll(message);
    }

    // Without this handler the server's approval had no path back to the game layer,
    // so the encounter the server authorised was silently discarded on the client.
    private void Handle(MessagePayload<NetworkStartPartyEncounter> obj)
    {
        var payload = obj.What;
        var message = new StartPartyEncounterCommand(payload.AttackerPartyId, payload.DefenderPartyId);

        messageBroker.Publish(this, message);
    }
}
