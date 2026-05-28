using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct CompleteTransfer : ICommand
{
    [ProtoMember(1)]
    public readonly string TargetItemRosterId;

    [ProtoMember(2)]
    public readonly EquipmentElement EquipmentElement;

    [ProtoMember(3)]
    public readonly int Count;

    public CompleteTransfer(
        string targetItemRosterId,
        EquipmentElement equipmentElement,
        int count)
    {
        TargetItemRosterId = targetItemRosterId;
        EquipmentElement = equipmentElement;
        Count = count;
    }
}