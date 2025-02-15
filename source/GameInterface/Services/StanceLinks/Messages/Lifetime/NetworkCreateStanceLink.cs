using Common.Messaging;
using GameInterface.Services.Stances.Data;
using ProtoBuf;

namespace GameInterface.Services.Stances.Messages.Lifetime;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateStanceLink : ICommand
{
    public NetworkCreateStanceLink(StanceLinkCreationData data)
    {
        Data = data;
    }

    [ProtoMember(1)]
    public StanceLinkCreationData Data { get; }
}