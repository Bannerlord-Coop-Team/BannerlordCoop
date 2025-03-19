using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Template.Messages;
/// <summary>
/// TODO update summary
/// A command changes the state of something
/// </summary>
// We use Protobuf for our serializer for network transferable classes
[ProtoContract]
public record TemplateNetworkMessage : ICommand
{

    // This data needs to be natively serializable or have a registered protobuf surrogate
    [ProtoMember(1)]
    public string NetworkId { get; }

    [ProtoMember(2)]
    public float Value { get; }

    public TemplateNetworkMessage(string networkId, float value)
    {
        NetworkId = networkId;
        Value = value;
    }
}