using ProtoBuf;

namespace GameInterface.Services.ItemComponents.Data;

[ProtoContract(SkipConstructor = true)]
public record ItemComponentData(int TypeIndex, string Id)
{
    [ProtoMember(1)]
    public int TypeIndex = TypeIndex;

    [ProtoMember(2)]
    public string Id { get; } = Id;
}
