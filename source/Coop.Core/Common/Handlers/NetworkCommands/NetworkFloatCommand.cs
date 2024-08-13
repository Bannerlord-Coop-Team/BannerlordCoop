using ProtoBuf;
using ProtoBuf.Meta;

namespace Coop.Core.Common.Handlers.NetworkCommands;

[ProtoContract(SkipConstructor = true)]
public record NetworkFloatCommand : AbstractNetworkCommand<float>
{
    [ProtoMember(1)]
    public override float Value { get; }
    
    [ProtoMember(2)]
    public override string Id { get; }
    
    [ProtoMember(3)]
    public override string Target { get; }
    
    public NetworkFloatCommand(string id, float value, string target)
    {
        Id = id;
        Value = value;
        Target = target;
    }
}