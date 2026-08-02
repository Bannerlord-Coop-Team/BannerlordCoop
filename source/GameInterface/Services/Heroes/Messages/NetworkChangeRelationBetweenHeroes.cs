using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Heroes.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkChangeRelationBetweenHeroes : ICommand
{
    [ProtoMember(1)]
    public readonly string Hero1Id;
    [ProtoMember(2)]
    public readonly string Hero2Id;
    [ProtoMember(3)]
    public readonly int Relation;

    public NetworkChangeRelationBetweenHeroes(string hero1Id, string hero2Id, int relation)
    {
        Hero1Id = hero1Id;
        Hero2Id = hero2Id;
        Relation = relation;
    }
}
