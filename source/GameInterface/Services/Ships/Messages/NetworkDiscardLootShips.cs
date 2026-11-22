using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Ships.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkDiscardLootShips : ICommand
{
    [ProtoMember(1)]
    public readonly uint PartyHandle;

    [ProtoMember(2)]
    public readonly uint[] LootShipHandles;

    public NetworkDiscardLootShips(uint partyHandle, uint[] lootShipHandles)
    {
        PartyHandle = partyHandle;
        LootShipHandles = lootShipHandles;
    }
}
