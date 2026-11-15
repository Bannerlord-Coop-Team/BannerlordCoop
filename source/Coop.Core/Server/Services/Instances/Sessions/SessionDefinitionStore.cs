using System;

namespace Coop.Core.Server.Services.Instances.Sessions;

/// <summary>What a session server hosts: the session id, the mod that owns it and the mod's opaque payload.</summary>
public sealed class SessionDefinition
{
    public SessionDefinition(string sessionId, string modId, byte[] payload)
    {
        SessionId = sessionId;
        ModId = modId;
        Payload = payload ?? Array.Empty<byte>();
    }

    public string SessionId { get; }
    public string ModId { get; }

    /// <summary>Opaque to Coop: the mod defines and reads it.</summary>
    public byte[] Payload { get; }
}

/// <summary>[Server] The one session a campaign-free server hosts. A campaign server never opens one.</summary>
public interface ISessionDefinitionStore
{
    /// <summary>Null on a campaign server, and after <see cref="Close"/>.</summary>
    SessionDefinition Current { get; }

    /// <summary>True once <see cref="Open"/> ran, and stays true after <see cref="Close"/>.</summary>
    bool IsSessionServer { get; }

    /// <summary>Creates the session id and opens the session. Throws when one is already open.</summary>
    SessionDefinition Open(string modId, byte[] payload);

    /// <summary>Closes the session. False when <paramref name="sessionId"/> is not the current session.</summary>
    bool Close(string sessionId);
}

/// <inheritdoc cref="ISessionDefinitionStore"/>
public class SessionDefinitionStore : ISessionDefinitionStore
{
    private readonly object gate = new object();
    private SessionDefinition current;
    private bool isSessionServer;

    public SessionDefinition Current
    {
        get
        {
            lock (gate)
            {
                return current;
            }
        }
    }

    public bool IsSessionServer
    {
        get
        {
            lock (gate)
            {
                return isSessionServer;
            }
        }
    }

    public SessionDefinition Open(string modId, byte[] payload)
    {
        lock (gate)
        {
            if (current != null)
                throw new InvalidOperationException("A co-op session is already open");

            current = new SessionDefinition(Missions.Sessions.SessionInstanceId.Create(), modId, payload);
            isSessionServer = true;
            return current;
        }
    }

    public bool Close(string sessionId)
    {
        lock (gate)
        {
            if (current == null || current.SessionId != sessionId)
                return false;

            current = null;
            return true;
        }
    }
}
