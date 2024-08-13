using ProtoBuf;

namespace Common.Messaging;

/// <summary>
/// A command with a source ID and a property or field as a target.
/// <see cref="IEvent"/>
/// </summary>
/// <inheritdoc/>
public interface ITargetCommand : ICommand
{
    public string Id { get; }
    
    public string Target { get; }
}