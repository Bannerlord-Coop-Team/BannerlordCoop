using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Enables time controls on a client
/// </summary>
[ProtoContract]
public record NetworkEnableTimeControls : ICommand
{
}

/// <summary>
/// Disables time controls on a client
/// </summary>
[ProtoContract]
public record NetworkDisableTimeControls : ICommand
{
}
