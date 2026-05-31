using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct SlaughterItem : ICommand
{
    [ProtoMember(1)]
    public readonly string TargetItemRosterId;

    [ProtoMember(2)]
    public readonly EquipmentElement EquipmentElement;

    [ProtoMember(3)]
    public readonly int MeatCount;

    [ProtoMember(4)]
    public readonly int HideCount;

    public SlaughterItem(
        string targetItemRosterId,
        EquipmentElement equipmentElement,
        int meatCount, int hideCount)
    {
        TargetItemRosterId = targetItemRosterId;
        EquipmentElement = equipmentElement;
        MeatCount = meatCount;
        HideCount = hideCount;
    }
}