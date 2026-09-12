using Common.Messaging;

namespace Coop.Core.Server.Services.Save.Messages;

/// <summary>Signals that every saved player registration has been restored.</summary>
internal readonly struct SavedPlayerRegistrationsRestored : IEvent
{
}
