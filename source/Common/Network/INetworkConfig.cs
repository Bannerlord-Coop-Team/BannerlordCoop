using LiteNetLib;
using System;
using System.Net;

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
    IPAddress LanAddress { get; }
    int LanPort { get; }
    IPAddress WanAddress { get; }
    int WanPort { get; }
    TimeSpan PingInterval { get; }
    TimeSpan ReconnectDelay { get; }
    TimeSpan DisconnectTimeout { get; }
    NatAddressType NATType { get; }

    void SetRendezvous(string address, int port);
}
