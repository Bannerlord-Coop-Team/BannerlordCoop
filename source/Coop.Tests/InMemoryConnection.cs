using Coop.Network;
using System;
using System.Collections.Generic;
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

        private readonly List<byte[]> sendBuffer = new List<byte[]>();

        public void SendRaw(ArraySegment<byte> raw)
        {
            sendBuffer.Add(raw.ToArray());
        }

        public void ExecuteSends()
        {
            sendBuffer.ForEach(buffer => OnSend?.Invoke(buffer));
            sendBuffer.Clear();
        }
    }
}
