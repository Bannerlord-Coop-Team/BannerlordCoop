using Common.Messaging;
using GameInterface.Services.Inventory.Data;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkNotifyCaravanTransaction : ICommand
{
    [ProtoMember(1)]
    public readonly string CaravanPartyId;

    [ProtoMember(2)]
    public readonly string TownId;

    [ProtoMember(3)]
    public readonly (ItemObjectData, int)[] Items;

    public NetworkNotifyCaravanTransaction(string caravanPartyId, string townId, (ItemObjectData, int)[] items)
    {
        CaravanPartyId = caravanPartyId;
        TownId = townId;
        Items = items;
    }
}
