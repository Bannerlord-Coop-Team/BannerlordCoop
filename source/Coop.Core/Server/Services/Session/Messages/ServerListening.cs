using Common.Messaging;

namespace Coop.Core.Server.Services.Session.Messages;

/// <summary>Published once the server's network is bound and the campaign is running.</summary>
public record ServerListening : IEvent
{
}
