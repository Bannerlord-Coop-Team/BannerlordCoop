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
    /// <summary>
    /// True when the session is reached through a local tunnel pump (Steam P2P) instead of
    /// a direct address. Tunneled peers cannot NAT-punch each other, so mission traffic
    /// stays on the server relay.
    /// </summary>
    bool IsTunneled { get; }
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
    /// <summary>
    /// LiteNetLib's internal logic-thread cycle (NetManager.UpdateTime): resends, packet merging and
    /// reliable-window advances happen at this cadence.
    /// </summary>
    TimeSpan UpdateTime { get; }
    NatAddressType NATType { get; }
}
