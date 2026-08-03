using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.GameDebug.Messages;

[ProtoContract(SkipConstructor = true)]
public record NetworkRouteBattleEnemies : ICommand
{
    [ProtoMember(1)]
    public string MapEventId { get; }

    [ProtoMember(2)]
    public int EnemiesToLeaveFighting { get; }

    public NetworkRouteBattleEnemies(string mapEventId, int enemiesToLeaveFighting)
    {
        MapEventId = mapEventId;
        EnemiesToLeaveFighting = enemiesToLeaveFighting;
    }
}
