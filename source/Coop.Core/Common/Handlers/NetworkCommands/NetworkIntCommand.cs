using ProtoBuf;

namespace Coop.Core.Common.Handlers.NetworkCommands;

[ProtoContract(SkipConstructor = true)]
public record NetworkIntCommand : AbstractNetworkCommand<int>
{
    [ProtoMember(1)]
    public override int Value { get; }
    
    [ProtoMember(2)]
    public override string Id { get; }
    
    [ProtoMember(3)]
    public override string Target { get; }
    
    public NetworkIntCommand(string id, int value, string target)
    {
        Id = id;
        Value = value;
        Target = target;
    }
}