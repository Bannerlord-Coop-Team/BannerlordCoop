using LiteNetLib;
using Network.Infrastructure;

namespace Coop.NetImpl.LiteNet
{
    public static class NetManagerFactory
    {
        public static NetManager Create(INetEventListener listener, NetworkConfiguration config)
        {
            return new NetManager(listener)
            {
                MaxConnectAttempts = 20,
                ReconnectDelay = (int) config.ReconnectDelay.TotalMilliseconds,
                DisconnectTimeout = (int) config.DisconnectTimeout.TotalMilliseconds,
                UpdateTime = (int) config.UpdateTime.TotalMilliseconds
            };
        }
    }
}
