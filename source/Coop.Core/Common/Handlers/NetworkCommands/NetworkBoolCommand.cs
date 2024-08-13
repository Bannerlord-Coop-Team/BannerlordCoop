using ProtoBuf;
using ProtoBuf.Meta;

namespace Coop.Core.Common.Handlers.NetworkCommands;

[ProtoContract(SkipConstructor = true)]
public record NetworkBoolCommand : AbstractNetworkCommand<bool>
{
    [ProtoMember(1)]
    public override bool Value { get; }
    
    [ProtoMember(2)]
    public override string Id { get; }
    
    [ProtoMember(3)]
    public override string Target { get; }
    
    public NetworkBoolCommand(string id, bool value, string target)
    {
        Id = id;
        Value = value;
        Target = target;
    }
}