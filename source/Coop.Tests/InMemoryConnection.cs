using System;
using System.Collections.Generic;
using Network.Infrastructure;

namespace Coop.Tests
{
    public class InMemoryConnection : INetworkConnection
    {
        private readonly List<byte[]> sendBuffer = new List<byte[]>();

        public int FragmentLength => 100;

        public int MaxPackageLength => 100000;

        public int Latency => 0;

        public void Close(EDisconnectReason eReason)
        {
            throw new NotImplementedException();
        }

        public void SendRaw(ArraySegment<byte> raw, EDeliveryMethod _)
        {
            sendBuffer.Add(raw.ToArray());
        }

        public event Action<ArraySegment<byte>> OnSend;

        public void ExecuteSends()
        {
            sendBuffer.ForEach(buffer => OnSend?.Invoke(buffer));
            sendBuffer.Clear();
        }
    }
}
