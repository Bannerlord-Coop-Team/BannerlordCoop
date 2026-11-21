using Common;
using Common.Messaging;
using GameInterface.Registry.Auto;
using GameInterface.Services.AdvancedStart.Messages;
using GameInterface.Services.Inventory.Interfaces;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.AdvancedStart.Handlers;

/// <summary>
/// Client: applies a joining hero's equipment from <see cref="NetworkAdvancedStartApplied"/>.
/// Stopgap until equipment sync (#4786) lands; remove with that message.
/// </summary>
internal class AdvancedStartHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IInventoryLogicInterface inventoryLogicInterface;

    public AdvancedStartHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IInventoryLogicInterface inventoryLogicInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.inventoryLogicInterface = inventoryLogicInterface;
        messageBroker.Subscribe<NetworkAdvancedStartApplied>(Handle_NetworkAdvancedStartApplied);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkAdvancedStartApplied>(Handle_NetworkAdvancedStartApplied);
    }

    private void Handle_NetworkAdvancedStartApplied(MessagePayload<NetworkAdvancedStartApplied> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging(data.HeroId, out Hero hero)) return;
            if (!objectManager.TryGetObjectWithLogging(data.MobilePartyId, out MobileParty party)) return;

            var equipment = (data.Equipment ?? Array.Empty<EquipmentData>())
                .Select(set =>
                {
                    // A temporary copy for UpdateEquipmentWithData to read, not equipment the registry should track.
                    using var scope = new TransientEquipmentSyncScope();
                    var copy = new Equipment(set.EquipmentType);
                    for (int i = 0; i < Equipment.EquipmentSlotLength; i++)
                        copy._itemSlots[i] = set.ItemSlots[i];
                    return copy;
                })
                .ToArray();

            // Writes the slots and rebuilds the party visual, as a finished inventory screen does.
            inventoryLogicInterface.UpdateEquipmentWithData(
                party, new Dictionary<CharacterObject, Equipment[]> { [hero.CharacterObject] = equipment }, hero);
        }, context: nameof(Handle_NetworkAdvancedStartApplied));
    }
}
