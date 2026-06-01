using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.Core;
using static TaleWorlds.Core.Equipment;

namespace GameInterface.Services.Inventory.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct ResetEquipment : ICommand
{
    [ProtoMember(1)]
    public readonly Dictionary<string, Dictionary<EquipmentType, EquipmentElement[]>> HeroIdEquipmentElements;

    public ResetEquipment(
        Dictionary<string, Dictionary<EquipmentType, EquipmentElement[]>> heroIdEquipmentElements)
    {
        HeroIdEquipmentElements = heroIdEquipmentElements;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct ResetEquipmentClients : ICommand
{
    [ProtoMember(1)]
    public readonly Dictionary<string, Dictionary<EquipmentType, EquipmentElement[]>> HeroIdEquipmentElements;

    public ResetEquipmentClients(
        Dictionary<string, Dictionary<EquipmentType, EquipmentElement[]>> heroIdEquipmentElements)
    {
        HeroIdEquipmentElements = heroIdEquipmentElements;
    }
}