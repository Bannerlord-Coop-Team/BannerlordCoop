using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyComponents.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyComponents.Handlers;

internal class CaravanPartyComponentHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    private static readonly ILogger Logger = LogManager.GetLogger<CaravanPartyComponentHandler>();

    public CaravanPartyComponentHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<CaravanPartyComponentInitArgsUpdated>(Handle_CaravanPartyComponentInitArgsUpdated);
        messageBroker.Subscribe<NetworkUpdateCaravanPartyComponentInitArgs>(Handle_NetworkUpdateCaravanPartyComponentInitArgs);

        messageBroker.Subscribe<CaravanPartyOwnerChanged>(Handle_CaravanPartyOwnerChanged);
        messageBroker.Subscribe<NetworkCaravanPartyOwnerChanged>(Handle_NetworkCaravanPartyOwnerChanged);

        messageBroker.Subscribe<CaravanPartySettlementChanged>(Handle_CaravanPartySettlementChanged);
        messageBroker.Subscribe<NetworkCaravanPartySettlementChanged>(Handle_NetworkCaravanPartySettlementChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CaravanPartyComponentInitArgsUpdated>(Handle_CaravanPartyComponentInitArgsUpdated);
        messageBroker.Unsubscribe<NetworkUpdateCaravanPartyComponentInitArgs>(Handle_NetworkUpdateCaravanPartyComponentInitArgs);

        messageBroker.Unsubscribe<CaravanPartyOwnerChanged>(Handle_CaravanPartyOwnerChanged);
        messageBroker.Unsubscribe<NetworkCaravanPartyOwnerChanged>(Handle_NetworkCaravanPartyOwnerChanged);

        messageBroker.Unsubscribe<CaravanPartySettlementChanged>(Handle_CaravanPartySettlementChanged);
        messageBroker.Unsubscribe<NetworkCaravanPartySettlementChanged>(Handle_NetworkCaravanPartySettlementChanged);
    }

    private void Handle_CaravanPartyOwnerChanged(MessagePayload<CaravanPartyOwnerChanged> payload)
    {
        var instance = payload.What.Instance;
        var owner = payload.What.Owner;

        if (!objectManager.TryGetIdWithLogging(instance, out var caravanPartyComponentId)) return;

        // Owner may legitimately be null.
        string ownerId = null;
        if (owner != null && !objectManager.TryGetIdWithLogging(owner, out ownerId)) return;

        network.SendAll(new NetworkCaravanPartyOwnerChanged(caravanPartyComponentId, ownerId));
    }

    private void Handle_NetworkCaravanPartyOwnerChanged(MessagePayload<NetworkCaravanPartyOwnerChanged> payload)
    {
        var message = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<CaravanPartyComponent>(message.CaravanPartyComponentId, out var instance)) return;

            Hero owner = null;
            if (message.OwnerId != null &&
                !objectManager.TryGetObjectWithLogging(message.OwnerId, out owner)) return;

            using (new AllowedThread())
            {
                instance.Owner = owner;
            }
        });
    }

    private void Handle_CaravanPartySettlementChanged(MessagePayload<CaravanPartySettlementChanged> payload)
    {
        var instance = payload.What.Instance;
        var settlement = payload.What.Settlement;

        if (!objectManager.TryGetIdWithLogging(instance, out var caravanPartyComponentId)) return;
        if (!objectManager.TryGetIdWithLogging(settlement, out var settlementId)) return;

        network.SendAll(new NetworkCaravanPartySettlementChanged(caravanPartyComponentId, settlementId));
    }

    private void Handle_NetworkCaravanPartySettlementChanged(MessagePayload<NetworkCaravanPartySettlementChanged> payload)
    {
        var message = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<CaravanPartyComponent>(message.CaravanPartyComponentId, out var instance)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(message.SettlementId, out var settlement)) return;

            using (new AllowedThread())
            {
                instance.Settlement = settlement;
            }
        });
    }

    private void Handle_CaravanPartyComponentInitArgsUpdated(MessagePayload<CaravanPartyComponentInitArgsUpdated> payload)
    {
        var instance = payload.What.Instance;
        var initArgs = payload.What.InitArgs;

        if (!objectManager.TryGetIdWithLogging(instance, out var caravanPartyComponentId)) return;

        // caravanLeader may legitimately be null
        string caravanLeaderId = null;
        if (initArgs.CaravanLeader != null && !objectManager.TryGetIdWithLogging(initArgs.CaravanLeader, out caravanLeaderId)) return;

        string caravanItemRosterId = null;
        if (initArgs.CaravanItems != null && !objectManager.TryGetIdWithLogging(initArgs.CaravanItems, out caravanItemRosterId)) return;
        if (!objectManager.TryGetIdWithLogging(initArgs.PartyTemplateObject, out var partyTemplateObjectId)) return;

        network.SendAll(new NetworkUpdateCaravanPartyComponentInitArgs(
            caravanPartyComponentId,
            caravanLeaderId,
            caravanItemRosterId,
            partyTemplateObjectId
        ));
    }

    private void Handle_NetworkUpdateCaravanPartyComponentInitArgs(MessagePayload<NetworkUpdateCaravanPartyComponentInitArgs> payload)
    {
        var message = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<CaravanPartyComponent>(message.CaravanPartyComponentId, out var instance)) return;

            Hero caravanLeader = null;
            if (message.CaravanLeaderId != null && !objectManager.TryGetObjectWithLogging<Hero>(message.CaravanLeaderId, out caravanLeader)) return;

            ItemRoster caravanItems = null;
            if (message.CaravanItemRosterId != null && !objectManager.TryGetObjectWithLogging<ItemRoster>(message.CaravanItemRosterId, out caravanItems)) return;
            if (!objectManager.TryGetObjectWithLogging<PartyTemplateObject>(message.PartyTemplateObjectId, out var partyTemplateObject)) return;

            using (new AllowedThread())
            {
                var initArgs = new CaravanPartyComponent.InitializationArgs(partyTemplateObject, caravanLeader, caravanItems);

                instance._initializationArgs = initArgs;
            }
        });
    }
}
