using System;

namespace Common.Network;

public interface INetworkConfiguration
{
    string Address { get; }
    int Port { get; }
    string Token { get; }
    string P2PToken { get; }
    TimeSpan ConnectionTimeout { get; }
    /// <summary>
    /// Per-client outgoing packet backlog at which the server fully pauses time until the client catches up.
    /// </summary>
    int MaxPacketsInQueue { get; }
    /// <summary>
    /// Per-client outgoing packet backlog at which the server caps time at 1x, below
    /// <see cref="MaxPacketsInQueue"/>, to let the client catch up without a full pause.
    /// </summary>
    int SlowDownPacketThreshold { get; }
    TimeSpan AuditTimeout { get; }
    TimeSpan ObjectCreationTimeout { get; }
    TimeSpan NetworkPollInterval { get; }
}
