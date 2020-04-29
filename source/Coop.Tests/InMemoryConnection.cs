using Coop.Network;
using System;
using Coop.Multiplayer;

namespace Coop.Tests
{
    public class InMemoryConnection : INetworkConnection
    {
        public event Action<ArraySegment<byte>> OnSend;

        public InMemoryConnection()
        {
        }

        public int FragmentLength => 100;

        public int MaxPackageLength => 100000;

        public int Latency => 0;

        public void Close(EDisconnectReason eReason)
        {
            throw new NotImplementedException();
        }

        public void SendRaw(ArraySegment<byte> raw)
        {
            OnSend?.Invoke(raw);
        }
    }
}
