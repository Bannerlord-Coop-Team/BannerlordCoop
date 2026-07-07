using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEventComponents.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRaidLootedItemsUpdated : ICommand
{
    [ProtoMember(1)]
    public readonly string PartyId;
    [ProtoMember(2)]
    public readonly string[] ItemIds;
    [ProtoMember(3)]
    public readonly int[] Amounts;

    public NetworkRaidLootedItemsUpdated(string partyId, string[] itemIds, int[] amounts)
    {
        PartyId = partyId;
        ItemIds = itemIds;
        Amounts = amounts;
    }
}
