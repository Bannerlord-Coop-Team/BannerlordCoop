using ProtoBuf;

namespace GameInterface.Services.PartyComponents.Data;

[ProtoContract(SkipConstructor = true)]
public record PartyComponentData
{
    public string Id { get; }

    public PartyComponentData(string id)
    {
        Id = id;
    }
}
