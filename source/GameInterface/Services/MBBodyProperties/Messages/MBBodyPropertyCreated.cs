using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MBBodyProperties.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateMBBodyProperty : ICommand
{
    [ProtoMember(1)]
    public string Id { get; }
    public NetworkCreateMBBodyProperty(string id)
    {
        Id = id;
    }
}
