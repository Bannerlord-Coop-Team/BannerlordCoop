using Common.Messaging;

namespace Coop.Core.Server.Services.Instances.Sessions;

/// <summary>[Server, local] The host closed the session and every member was told to end the mission.</summary>
public class SessionClosed : IEvent
{
    public SessionClosed(string sessionId)
    {
        SessionId = sessionId;
    }

    public string SessionId { get; }
}
