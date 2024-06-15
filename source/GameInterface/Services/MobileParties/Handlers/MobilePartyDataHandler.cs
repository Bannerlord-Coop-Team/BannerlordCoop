using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Data;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.MobileParties.Handlers;

/// <summary>
/// Handler for party related messages.
/// </summary>
internal class MobilePartyDataHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyDataHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public MobilePartyDataHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        messageBroker.Subscribe<ChangePartyArmy>(Handle_ChangePartyArmy);
        messageBroker.Subscribe<PartyComponentChanged>(Handle_PartyComponentChanged);
        messageBroker.Subscribe<NetworkChangePartyComponent>(Handle_ChangePartyComponent);

        messageBroker.Subscribe<ActualClanChanged>(Handle_ActualClanChanged);
        messageBroker.Subscribe<NetworkChangeActualClan>(Handle_ChangeActualClan);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ChangePartyArmy>(Handle_ChangePartyArmy);
        messageBroker.Unsubscribe<PartyComponentChanged>(Handle_PartyComponentChanged);
        messageBroker.Unsubscribe<NetworkChangePartyComponent>(Handle_ChangePartyComponent);

        messageBroker.Unsubscribe<ActualClanChanged>(Handle_ActualClanChanged);
        messageBroker.Unsubscribe<NetworkChangeActualClan>(Handle_ChangeActualClan);
    }

    private void Handle_ChangePartyArmy(MessagePayload<ChangePartyArmy> payload)
    {
        var partyId = payload.What.Data.PartyId;
        var armyId = payload.What.Data.ArmyId;

        if (objectManager.TryGetObject(partyId, out MobileParty party) == false)
        {
            Logger.Error("Failed to find party with stringId {stringId}", partyId);
            return;
        }

        if (objectManager.TryGetObject(armyId, out Army army) == false)
        {
            Logger.Error("Failed to find army with stringId {stringId}", armyId);
            return;
        }

        PartyArmyPatches.OverrideSetArmy(party, army);
    }

    private void Handle_PartyComponentChanged(MessagePayload<PartyComponentChanged> payload)
    {
        var message = new NetworkChangePartyComponent(
            payload.What.PartyId,
            payload.What.ComponentId
        );

        network.SendAll(message);
    }

    private void Handle_ChangePartyComponent(MessagePayload<NetworkChangePartyComponent> payload)
    {
        var partyId = payload.What.PartyId;
        var componentId = payload.What.PartyComponentId;

        if (objectManager.TryGetObject(partyId, out MobileParty party) == false)
        {
            Logger.Error("Failed to find party with stringId {stringId}", partyId);
            return;
        }

        if (objectManager.TryGetObject(componentId, out PartyComponent component) == false)
        {
            Logger.Error("Failed to find PartyComponent with stringId {stringId}", componentId);
            return;
        }

        objectManager.Remove(componentId);

        party._partyComponent = component;
    }

    private void Handle_ActualClanChanged(MessagePayload<ActualClanChanged> payload)
    {
        var message = new NetworkChangeActualClan(
            payload.What.PartyId,
            payload.What.ClanId
        );

        network.SendAll(message);
    }

    private void Handle_ChangeActualClan(MessagePayload<NetworkChangeActualClan> payload)
    {
        var partyId = payload.What.PartyId;
        var clanId = payload.What.ClanId;

        if (objectManager.TryGetObject(partyId, out MobileParty party) == false)
        {
            Logger.Error("Failed to find party with stringId {stringId}", partyId);
            return;
        }

        // Set clan to null if id was null
        if (clanId == null)
        {
            ActualClanPatches.OverrideSetActualClan(party, null);
            return;
        }

        if (objectManager.TryGetObject(clanId, out Clan clan) == false)
        {
            Logger.Error("Failed to find Clan with stringId {stringId}", clanId);
            return;
        }

        ActualClanPatches.OverrideSetActualClan(party, clan);
    }
}
