using System;

namespace Common.Network;

public interface INetworkConfig
{
    string Address { get; }
    int Port { get; }
    string Token { get; }
    string P2PToken { get; }
    TimeSpan ConnectionTimeout { get; }
    int MaxPacketsInQueue { get; }
    int ResumePacketsInQueue { get; }
    TimeSpan AuditTimeout { get; }
    TimeSpan ObjectCreationTimeout { get; }
    TimeSpan NetworkPollInterval { get; }
}
