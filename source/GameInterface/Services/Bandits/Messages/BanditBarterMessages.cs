using Common.Messaging;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;
using System;

namespace GameInterface.Services.Bandits.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestBanditBarter : ICommand
{
    [ProtoMember(1)]
    public readonly string BanditPartyId;
    [ProtoMember(2)]
    public readonly int PlayerGold;
    [ProtoMember(3)]
    public readonly ItemRosterElementData[] PlayerItems;
    [ProtoMember(4)]
    public readonly TroopRosterElementData[] PlayerPrisoners;
    [ProtoMember(5)]
    public readonly string RequestId;

    public NetworkRequestBanditBarter(
        string banditPartyId,
        int playerGold,
        ItemRosterElementData[] playerItems,
        TroopRosterElementData[] playerPrisoners,
        string requestId = null)
    {
        BanditPartyId = banditPartyId;
        PlayerGold = playerGold;
        PlayerItems = playerItems ?? Array.Empty<ItemRosterElementData>();
        PlayerPrisoners = playerPrisoners ?? Array.Empty<TroopRosterElementData>();
        RequestId = requestId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkBanditBarterResult : ICommand
{
    [ProtoMember(1)]
    public readonly string BanditPartyId;
    [ProtoMember(2)]
    public readonly bool Accepted;
    [ProtoMember(3)]
    public readonly int PlayerGold;
    [ProtoMember(4)]
    public readonly string Reason;
    [ProtoMember(5)]
    public readonly string RequestId;

    public NetworkBanditBarterResult(
        string banditPartyId,
        bool accepted,
        int playerGold,
        string reason = null,
        string requestId = null)
    {
        BanditPartyId = banditPartyId;
        Accepted = accepted;
        PlayerGold = playerGold;
        Reason = reason;
        RequestId = requestId;
    }
}
