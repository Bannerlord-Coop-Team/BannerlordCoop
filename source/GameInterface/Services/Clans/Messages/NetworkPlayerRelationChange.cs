using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerRelationChange : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;
    [ProtoMember(2)]
    public readonly int Relation;

    public NetworkPlayerRelationChange(string heroId, int relation)
    {
        this.HeroId = heroId;
        Relation = relation;
    }
}
