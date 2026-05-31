using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct CompleteTransfer : ICommand
{
    [ProtoMember(1)]
    public readonly string FromItemRosterId;

    [ProtoMember(2)]
    public readonly string ToItemRosterId;

    [ProtoMember(3)]
    public readonly EquipmentElement EquipmentElement;

    [ProtoMember(4)]
    public readonly int Count;

    public CompleteTransfer(
        string fromItemRosterId,
        string toItemRosterId,
        EquipmentElement equipmentElement,
        int count)
    {
        FromItemRosterId = fromItemRosterId;
        ToItemRosterId = toItemRosterId;
        EquipmentElement = equipmentElement;
        Count = count;
    }
}