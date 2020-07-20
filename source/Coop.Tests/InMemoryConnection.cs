using System;
using System.Collections.Generic;
using Network.Infrastructure;

namespace Coop.Tests
{
    public class InMemoryConnection : INetworkConnection
    {
        public List<byte[]> SendBuffer { get; } = new List<byte[]>();

        public int FragmentLength => 100;

        public int MaxPackageLength => 100000;

        public int Latency => 0;

        public void Close(EDisconnectReason eReason)
        {
            throw new NotImplementedException();
        }

        public void SendRaw(ArraySegment<byte> raw, EDeliveryMethod _)
        {
            SendBuffer.Add(raw.ToArray());
        }

        public event Action<ArraySegment<byte>> OnSend;

        public void ExecuteSends()
        {
            SendBuffer.ForEach(buffer => OnSend?.Invoke(buffer));
            SendBuffer.Clear();
        }
    }
}
