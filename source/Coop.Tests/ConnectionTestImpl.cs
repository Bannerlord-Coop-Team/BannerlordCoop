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
        public ConnectionTestImpl() : base(
            new InMemoryConnection(),
            new GameStatePersistenceTestImpl())
        {
        }

        public InMemoryConnection NetworkImpl => Network as InMemoryConnection;

        public GameStatePersistenceTestImpl GameStatePersistenceImpl =>
            GameStatePersistence as GameStatePersistenceTestImpl;

        public EConnectionState StateImpl { get; set; }
        public override EConnectionState State => StateImpl;

        public override void Disconnect(EDisconnectReason eReason)
        {
            throw new NotImplementedException();
        }
    }
}
