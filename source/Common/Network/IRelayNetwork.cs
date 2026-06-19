using System.Net;

namespace Common.Network;

public interface IRelayNetwork : INetwork
{
    IPEndPoint ServerEndpoint { get; }
}
