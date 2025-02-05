using ProtoBuf;

namespace GameInterface.Services.PartyComponents.Data;

[ProtoContract(SkipConstructor = true)]
public record PartyComponentData(int TypeIndex, string Id)
{
    [ProtoMember(1)]
    public int TypeIndex = TypeIndex;

    [ProtoMember(2)]
    public string Id { get; } = Id;
}
