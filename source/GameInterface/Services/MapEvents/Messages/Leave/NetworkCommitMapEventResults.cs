using Common.Messaging;
using GameInterface.Services.MapEvents.Data;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Messages.Leave;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCommitMapEventResults : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    /// <summary>
    /// The side that won, from the server's authoritative battle state. Carried in the message because only
    /// the client that committed the battle result knows the state locally — allied winners never see it.
    /// </summary>
    [ProtoMember(2)]
    public readonly BattleSideEnum WinningSide;

    [ProtoMember(3)]
    public readonly NetworkPlayerLootData PlayerLootData;

    /// <summary>The server-authored map-event party whose rewards this receiver must stage.</summary>
    [ProtoMember(4)]
    public readonly string PlayerMapEventPartyId;

    /// <summary>The receiver's server-authoritative side in this map event.</summary>
    [ProtoMember(5)]
    public readonly BattleSideEnum PlayerSide;

    public NetworkCommitMapEventResults(
        string mapEventId,
        BattleSideEnum winningSide,
        BattleSideEnum playerSide,
        string playerMapEventPartyId,
        NetworkPlayerLootData playerLootData)
    {
        MapEventId = mapEventId;
        WinningSide = winningSide;
        PlayerSide = playerSide;
        PlayerMapEventPartyId = playerMapEventPartyId;
        PlayerLootData = playerLootData;
    }
}
