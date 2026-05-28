using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using static TaleWorlds.Core.Equipment;

namespace GameInterface.Services.Inventory.Handlers;

internal class ResetEquipmentHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<ResetEquipmentHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public ResetEquipmentHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<EquipmentReset>(Handle_EquipmentReset);
        messageBroker.Subscribe<ResetEquipment>(Handle_ResetEquipment);
        messageBroker.Subscribe<ResetEquipmentClients>(Handle_ResetEquipmentClients);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<EquipmentReset>(Handle_EquipmentReset);
        messageBroker.Unsubscribe<ResetEquipment>(Handle_ResetEquipment);
        messageBroker.Unsubscribe<ResetEquipmentClients>(Handle_ResetEquipmentClients);
    }

    private void Handle_EquipmentReset(MessagePayload<EquipmentReset> obj)
    {
        var characterEquipments = obj.What.CharacterEquipments;

        Dictionary<string, Dictionary<EquipmentType, EquipmentElement[]>> heroIdEquipmentElements = new();
        foreach (KeyValuePair<CharacterObject, Equipment[]> keyValuePair in characterEquipments)
        {
            if (!objectManager.TryGetIdWithLogging(keyValuePair.Key.HeroObject, out var heroId)) continue;

            heroIdEquipmentElements[heroId] = new Dictionary<EquipmentType, EquipmentElement[]>();
            foreach (Equipment equipment in keyValuePair.Value)
            {
                heroIdEquipmentElements[heroId][equipment._equipmentType] = equipment._itemSlots;
            }
        }

        var message = new ResetEquipment(heroIdEquipmentElements);
        network.SendAll(message);
    }

    private void Handle_ResetEquipment(MessagePayload<ResetEquipment> obj)
    {
        ResetEquipmentInternal(obj.What.HeroIdEquipmentElements);

        var message = new ResetEquipmentClients(obj.What.HeroIdEquipmentElements);
        network.SendAll(message);
    }

    private void Handle_ResetEquipmentClients(MessagePayload<ResetEquipmentClients> obj)
    {
        ResetEquipmentInternal(obj.What.HeroIdEquipmentElements);
    }

    private void ResetEquipmentInternal(Dictionary<string, Dictionary<EquipmentType, EquipmentElement[]>> dictionary)
    {
        foreach (KeyValuePair<string, Dictionary<EquipmentType, EquipmentElement[]>> keyValuePair in dictionary)
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(keyValuePair.Key, out var hero)) continue;

            foreach (KeyValuePair<EquipmentType, EquipmentElement[]> equipment in keyValuePair.Value)
            {
                if (equipment.Key == EquipmentType.Battle)
                {
                    FillEquipment(hero.CharacterObject.FirstBattleEquipment, equipment.Key, equipment.Value);
                }
                else if (equipment.Key == EquipmentType.Civilian)
                {
                    FillEquipment(hero.CharacterObject.FirstCivilianEquipment, equipment.Key, equipment.Value);
                }
                else if (equipment.Key == EquipmentType.Stealth)
                {
                    FillEquipment(hero.CharacterObject.FirstStealthEquipment, equipment.Key, equipment.Value);
                }
            }

            hero.PartyBelongedTo.Party.SetVisualAsDirty();
        }
    }

    private void FillEquipment(Equipment equipment, EquipmentType incomingEquipmentType, EquipmentElement[] incomingEquipmentElements)
    {
        equipment._equipmentType = incomingEquipmentType;
        for (int i = 0; i < 12; i++)
        {
            equipment[i] = incomingEquipmentElements[i];
        }
    }
}
