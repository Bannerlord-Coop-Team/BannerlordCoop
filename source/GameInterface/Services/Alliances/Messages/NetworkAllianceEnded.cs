using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Alliances.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAllianceEnded : ICommand
{
    [ProtoMember(1)]
    public readonly string Kingdom1Id;
    [ProtoMember(2)]
    public readonly string Kingdom2Id;

    public NetworkAllianceEnded(string kingdom1Id, string kingdom2Id)
    {
        Kingdom1Id = kingdom1Id;
        Kingdom2Id = kingdom2Id;
    }
}
