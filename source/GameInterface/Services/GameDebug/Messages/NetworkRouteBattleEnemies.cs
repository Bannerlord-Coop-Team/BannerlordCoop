using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.GameDebug.Messages;

[ProtoContract(SkipConstructor = true)]
public record NetworkRouteBattleEnemies : ICommand
{
    [ProtoMember(1)]
    public string MapEventId { get; }

    [ProtoMember(2)]
    public int EnemiesToLeaveFighting { get; }

    [ProtoMember(3)]
    public BattleSideEnum? Side { get; }

    public NetworkRouteBattleEnemies(string mapEventId, int enemiesToLeaveFighting, BattleSideEnum? side = null)
    {
        MapEventId = mapEventId;
        EnemiesToLeaveFighting = enemiesToLeaveFighting;
        Side = side;
    }
}
