using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyComponents.Messages;

[ProtoContract(SkipConstructor = true)]
public record NetworkUpdateCustomPartyComponent(string ComponentId, int CustomPartyComponentType, string Value) : ICommand
{
    [ProtoMember(1)]
    public string ComponentId { get; } = ComponentId;
    [ProtoMember(2)]
    public int CustomPartyComponentType { get; } = CustomPartyComponentType;
    [ProtoMember(3)]
    public string Value { get; } = Value;
}