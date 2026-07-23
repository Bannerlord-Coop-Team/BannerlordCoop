using Common.Messaging;

namespace Coop.Core.Common.Session.Messages;

/// <summary>
/// The spawned co-op server process exited, cleanly or not.
/// </summary>
public record HostedServerExited : IEvent
{
}
