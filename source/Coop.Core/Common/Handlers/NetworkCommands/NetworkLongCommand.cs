using ProtoBuf;

namespace Coop.Core.Common.Handlers.NetworkCommands;

[ProtoContract(SkipConstructor = true)]
public record NetworkLongCommand : AbstractNetworkCommand<long>
{
    [ProtoMember(1)]
    public override long Value { get; }
    
    [ProtoMember(2)]
    public override string Id { get; }
    
    [ProtoMember(3)]
    public override string Target { get; }
    
    public NetworkLongCommand(string id, long value, string target)
    {
        Id = id;
        Value = value;
        Target = target;
    }
}