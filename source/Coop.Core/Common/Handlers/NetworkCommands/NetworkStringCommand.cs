using Coop.Core.Common.Handlers.NetworkCommands.MBTypes;
using ProtoBuf;
using ProtoBuf.Meta;

namespace Coop.Core.Common.Handlers.NetworkCommands;

[ProtoContract(SkipConstructor = true)]
public record NetworkStringCommand : AbstractNetworkCommand<string>
{
    [ProtoMember(1)]
    public override string Value { get; }
    
    [ProtoMember(2)]
    public override string Id { get; }
    
    [ProtoMember(3)]
    public override string Target { get; }
    
    public NetworkStringCommand(string id, string value, string target)
    {
        Id = id;
        Value = value;
        Target = target;
    }
}