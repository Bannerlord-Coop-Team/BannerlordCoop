using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Common.Handlers.NetworkCommands.MBTypes;

[ProtoContract(SkipConstructor = true)]
public record NetworkTextObjectCommand : AbstractNetworkCommand<string>
{
    [ProtoMember(1)]
    public override string Value { get; }
    
    [ProtoMember(2)]
    public override string Id { get; }
    
    [ProtoMember(3)]
    public override string Target { get; }
    
    public NetworkTextObjectCommand(string id, string value, string target)
    {
        Id = id;
        Value = value;
        Target = target;
    }
    
}