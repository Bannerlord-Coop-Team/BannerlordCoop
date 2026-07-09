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

    public NetworkCommitMapEventResults(string mapEventId, BattleSideEnum winningSide, NetworkPlayerLootData playerLootData)
    {
        MapEventId = mapEventId;
        WinningSide = winningSide;
        PlayerLootData = playerLootData;
    }
}
