using System;

namespace Common.Network;

public interface INetworkConfiguration
{
    string Address { get; }
    int Port { get; }
    string Token { get; }
    string P2PToken { get; }
    TimeSpan ConnectionTimeout { get; }
    int MaxPacketsInQueue { get; }
}
