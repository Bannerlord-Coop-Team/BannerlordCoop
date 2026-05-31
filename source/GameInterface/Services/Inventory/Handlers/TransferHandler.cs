using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Handlers;

internal class TransferHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<TransferHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public TransferHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<TransferAttempted>(Handle_TransferAttempted);
        messageBroker.Subscribe<CompleteTransfer>(Handle_CompleteTransfer);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<TransferAttempted>(Handle_TransferAttempted);
        messageBroker.Unsubscribe<CompleteTransfer>(Handle_CompleteTransfer);
    }

    private void Handle_TransferAttempted(MessagePayload<TransferAttempted> obj)
    {
        string fromItemRosterId = null;
        if (obj.What.FromItemRoster != null && !objectManager.TryGetIdWithLogging(obj.What.FromItemRoster, out fromItemRosterId)) return;

        string toItemRosterId = null;
        if (obj.What.ToItemRoster != null && !objectManager.TryGetIdWithLogging(obj.What.ToItemRoster, out toItemRosterId)) return;

        var message = new CompleteTransfer(
            fromItemRosterId,
            toItemRosterId,
            obj.What.EquipmentElement,
            obj.What.Count);

        network.SendAll(message);
    }

    private void Handle_CompleteTransfer(MessagePayload<CompleteTransfer> obj)
    {
        ItemRoster fromItemRoster = null;
        if (obj.What.FromItemRosterId != null && !objectManager.TryGetObjectWithLogging(obj.What.FromItemRosterId, out fromItemRoster)) return;

        ItemRoster toItemRoster = null;
        if (obj.What.ToItemRosterId != null && !objectManager.TryGetObjectWithLogging(obj.What.ToItemRosterId, out toItemRoster)) return;

        // Helps prevent underflow exceptions for AddToCounts calls
        int fromCount = obj.What.Count;
        if (fromItemRoster != null)
        {
            int equipmentElementIndex = fromItemRoster.FindIndexOfElement(obj.What.EquipmentElement);
            if (equipmentElementIndex >= 0 && fromItemRoster[equipmentElementIndex].Amount - fromCount < 0)
            {
                fromCount = 0;
            }
        }

        fromItemRoster?.AddToCounts(obj.What.EquipmentElement, -fromCount);
        toItemRoster?.AddToCounts(obj.What.EquipmentElement, obj.What.Count);

        network.SendAll(new RefreshOtherInventory(obj.What.FromItemRosterId, obj.What.ToItemRosterId, new HashSet<EquipmentElement>() { obj.What.EquipmentElement }));
    }
}