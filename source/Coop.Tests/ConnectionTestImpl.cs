using System;
using Network.Infrastructure;

namespace Coop.Tests
{
    public class GameStatePersistenceTestImpl : IGameStatePersistence
    {
        public Action<ArraySegment<byte>> OnReceived;

        public void Receive(ArraySegment<byte> buffer)
        {
            OnReceived?.Invoke(buffer);
        }
    }

    public class ConnectionTestImpl : ConnectionBase
    {
        public ConnectionTestImpl(
            InMemoryConnection connection,
            IGameStatePersistence gameStatePersistence) : base(connection, gameStatePersistence)
        {
        }

        public InMemoryConnection NetworkImpl => Network as InMemoryConnection;

        public EConnectionState StateImpl { get; set; }
        public override EConnectionState State => StateImpl;

        public override void Disconnect(EDisconnectReason eReason)
        {
            throw new NotImplementedException();
        }
    }
}
