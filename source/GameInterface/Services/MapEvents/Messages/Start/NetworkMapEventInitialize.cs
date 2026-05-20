using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkMapEventInitialize : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly int BattleType;
    [ProtoMember(3)]
    public readonly string AttackerPartyId;
    [ProtoMember(4)]
    public readonly string DefenderPartyId;

    public NetworkMapEventInitialize(string mapEventId, int battleType, string attackerPartyId, string defenderPartyId)
    {
        MapEventId = mapEventId;
        BattleType = battleType;
        AttackerPartyId = attackerPartyId;
        DefenderPartyId = defenderPartyId;
    }
}
