using Common.Messaging;
using GameInterface.Services.PartyComponents.Data;
using ProtoBuf;

namespace GameInterface.Services.PartyComponents.Messages;

[ProtoContract(SkipConstructor = true)]
internal record NetworkCreatePartyComponent(PartyComponentData Data) : ICommand
{
    [ProtoMember(1)]
    public PartyComponentData Data { get; } = Data;
}
