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

    public NetworkMapEventInitialize(string mapEventId, int battleType)
    {
        MapEventId = mapEventId;
        BattleType = battleType;
    }
}
