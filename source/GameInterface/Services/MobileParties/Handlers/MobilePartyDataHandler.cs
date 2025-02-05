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
        messageBroker.Subscribe<PartyComponentChanged>(Handle_PartyComponentChanged);
        messageBroker.Subscribe<NetworkChangePartyComponent>(Handle_ChangePartyComponent);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyComponentChanged>(Handle_PartyComponentChanged);
        messageBroker.Unsubscribe<NetworkChangePartyComponent>(Handle_ChangePartyComponent);
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

        party._partyComponent = component;
    }
}