using Common.Messaging;
using GameInterface.Services.PartyComponents.Data;
using ProtoBuf;

namespace GameInterface.Services.PartyComponents.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreatePartyComponent : ICommand
{
    [ProtoMember(1)]
    public PartyComponentData Data { get; }

    public NetworkCreatePartyComponent(PartyComponentData data)
    {
        Data = data;
    }
}
