using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Heroes.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkHeroRelationChanged : ICommand
{
    [ProtoMember(1)]
    public string Hero1Id { get; }
    [ProtoMember(2)]
    public string Hero2Id { get; }
    [ProtoMember(3)]
    public int Value { get; }

    public NetworkHeroRelationChanged(string hero1Id, string hero2Id, int value)
    {
        Hero1Id = hero1Id;
        Hero2Id = hero2Id;
        Value = value;
    }
}
