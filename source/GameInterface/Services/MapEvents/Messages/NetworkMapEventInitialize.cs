using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages;

[ProtoContract(SkipConstructor = true)]
internal record NetworkMapEventInitialize : ICommand
{
    [ProtoMember(1)]
    public string MapEventId { get; }
    [ProtoMember(2)]
    public int BattleType { get; }
    [ProtoMember(3)]
    public string AttackerPartyId { get; }
    [ProtoMember(4)]
    public string DefenderPartyId { get; }

    public NetworkMapEventInitialize(string mapEventId, int battleType, string attackerPartyId, string defenderPartyId)
    {
        MapEventId = mapEventId;
        BattleType = battleType;
        AttackerPartyId = attackerPartyId;
        DefenderPartyId = defenderPartyId;
    }
}
