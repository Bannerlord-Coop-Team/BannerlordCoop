namespace Missions.Sessions;

/// <summary>The session a mission belongs to, passed to <see cref="CoopSessionController"/> as a resolve parameter.</summary>
internal sealed class SessionMissionContext
{
    public SessionMissionContext(string sessionId)
    {
        SessionId = sessionId;
    }

    public string SessionId { get; }
}
