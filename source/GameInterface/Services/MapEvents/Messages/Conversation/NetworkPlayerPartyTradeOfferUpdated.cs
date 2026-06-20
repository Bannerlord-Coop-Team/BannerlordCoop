using Common.Messaging;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Conversation;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerPartyTradeOfferUpdated : ICommand
{
    [ProtoMember(1)]
    public readonly string SessionId;
    [ProtoMember(2)]
    public readonly string PartyId;
    [ProtoMember(3)]
    public readonly ItemRosterElementData[] OfferedItems;
    [ProtoMember(4)]
    public readonly TroopRosterElementData[] OfferedTroops;
    [ProtoMember(5)]
    public readonly int OfferedGold;
    [ProtoMember(6)]
    public readonly string[] OfferedFiefs;
    [ProtoMember(7)]
    public readonly TroopRosterElementData[] OfferedPrisoners;

    public NetworkPlayerPartyTradeOfferUpdated(
        string sessionId,
        string partyId,
        ItemRosterElementData[] offeredItems,
        TroopRosterElementData[] offeredTroops,
        int offeredGold = 0,
        string[] offeredFiefs = null,
        TroopRosterElementData[] offeredPrisoners = null)
    {
        SessionId = sessionId;
        PartyId = partyId;
        OfferedItems = offeredItems ?? new ItemRosterElementData[0];
        OfferedTroops = offeredTroops ?? new TroopRosterElementData[0];
        OfferedGold = offeredGold;
        OfferedFiefs = offeredFiefs ?? new string[0];
        OfferedPrisoners = offeredPrisoners ?? new TroopRosterElementData[0];
    }
}
