using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Common.Handlers.NetworkCommands;

/// <summary>
/// Abstract network command to change a field or property.
/// </summary>
public abstract record AbstractNetworkCommand<TValue> : ITargetCommand
{
    [ProtoMember(1)]
    public abstract TValue Value { get; }
    [ProtoMember(2)]
    public abstract string Id { get; }
    
    [ProtoMember(3)]
    public abstract string Target { get; }
}