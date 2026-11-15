using System;

namespace Coop.Core.Server.States;

/// <summary>
/// Represents the state of the server
/// </summary>
public interface IServerState : IDisposable
{
    void Start();
    void Stop();
}
