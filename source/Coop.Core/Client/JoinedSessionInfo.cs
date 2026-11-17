namespace Coop.Core.Client;

/// <summary>What the session server told this client when it joined: the session, its mod and the mod's opaque payload.</summary>
public sealed class JoinedSessionInfo
{
    public JoinedSessionInfo(string sessionId, string modId, byte[] payload)
    {
        SessionId = sessionId;
        ModId = modId;
        Payload = payload;
    }

    public string SessionId { get; }
    public string ModId { get; }
    public byte[] Payload { get; }
}
