using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyComponents.Messages;

[ProtoContract(SkipConstructor = true)]
public record NetworkUpdateBanditPartyComponent(string ComponentId, int BanditPartyComponentType, string Value) : ICommand
{
    [ProtoMember(1)]
    public string ComponentId { get; } = ComponentId;
    [ProtoMember(2)]
    public int BanditPartyComponentType { get; } = BanditPartyComponentType;
    [ProtoMember(3)]
    public string Value { get; } = Value;
}