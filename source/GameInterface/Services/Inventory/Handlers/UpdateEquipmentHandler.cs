using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using static TaleWorlds.Core.Equipment;

namespace GameInterface.Services.Inventory.Handlers;

internal class UpdateEquipmentHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<UpdateEquipmentHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public UpdateEquipmentHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<EquipmentUpdated>(Handle_EquipmentUpdated);
        messageBroker.Subscribe<UpdateEquipment>(Handle_UpdateEquipment);
        messageBroker.Subscribe<UpdateEquipmentClients>(Handle_UpdateEquipmentClients);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<EquipmentUpdated>(Handle_EquipmentUpdated);
        messageBroker.Unsubscribe<UpdateEquipment>(Handle_UpdateEquipment);
        messageBroker.Unsubscribe<UpdateEquipmentClients>(Handle_UpdateEquipmentClients);
    }

    private void Handle_EquipmentUpdated(MessagePayload<EquipmentUpdated> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;

        var message = new UpdateEquipment(
            heroId,
            obj.What.EquipmentType,
            obj.What.EquipmentElement,
            obj.What.EquipmentIndex);

        network.SendAll(message);
    }

    private void Handle_UpdateEquipment(MessagePayload<UpdateEquipment> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;

        UpdateEquipmentInternal(hero.CharacterObject, obj.What.EquipmentType, obj.What.EquipmentIndex, obj.What.EquipmentElement);

        // Message for clients because equipment isn't managed
        var message = new UpdateEquipmentClients(
            obj.What.HeroId,
            obj.What.EquipmentType,
            obj.What.EquipmentElement,
            obj.What.EquipmentIndex);

        network.SendAll(message);
    }

    private void Handle_UpdateEquipmentClients(MessagePayload<UpdateEquipmentClients> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;

        // Don't run for hero that was modified by the client (already updated)
        if (hero == Hero.MainHero) return;

        UpdateEquipmentInternal(hero.CharacterObject, obj.What.EquipmentType, obj.What.EquipmentIndex, obj.What.EquipmentElement);
    }

    private void UpdateEquipmentInternal(
        CharacterObject character,
        EquipmentType equipmentType,
        EquipmentIndex equipmentIndex,
        EquipmentElement equipmentElement)
    {
        Equipment targetEquipment = null;
        if (equipmentType == EquipmentType.Battle)
        {
            targetEquipment = character.FirstBattleEquipment;
        }
        else if (equipmentType == EquipmentType.Civilian)
        {
            targetEquipment = character.FirstCivilianEquipment;
        }
        else if (equipmentType == EquipmentType.Stealth)
        {
            targetEquipment = character.FirstStealthEquipment;
        }

        if (targetEquipment != null) targetEquipment[equipmentIndex] = equipmentElement;

        character.HeroObject.PartyBelongedTo.Party.SetVisualAsDirty();
    }
}
