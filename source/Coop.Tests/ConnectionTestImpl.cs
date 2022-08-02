using System;
using Common;
using JetBrains.Annotations;
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

    public class ClientTestSM : CoopStateMachine<EClientConnectionState, EClientConnectionTrigger>
    {
        public ClientTestSM(EClientConnectionState StartingState) : base(StartingState)
        {
        }

        public void SetState(Enum state)
        {
            if (state.GetType() != typeof(EClientConnectionState))
            {
                throw new ArgumentException("wrong enum type");
            }

            State = state;
        }
    }

    public class ServerTestSM : CoopStateMachine<EServerConnectionState, EServerConnectionTrigger>
    {
        public ServerTestSM(EServerConnectionState StartingState) : base(StartingState)
        {
        }

        public void SetState(Enum state)
        {
            if (state.GetType() != typeof(EServerConnectionState))
            {
                throw new ArgumentException("wrong enum type");
            }

            State = state;
        }
    }

    public class ConnectionTestImpl : ConnectionBase
    {
        public enum EType
        {
            Client,
            Server
        }

            private readonly ClientTestSM m_ClientSM;
            private readonly ServerTestSM m_ServerSM;

        public ConnectionTestImpl(
            EType eType,
            InMemoryConnection connection,
            IGameStatePersistence gameStatePersistence) : base(connection, gameStatePersistence)
        {
            switch (eType)
            {
                case EType.Client:
                    m_ClientSM = new ClientTestSM(EClientConnectionState.JoinRequesting);
                    Dispatcher.RegisterStateMachine(this, m_ClientSM);
                    break;
                case EType.Server:
                    m_ServerSM = new ServerTestSM(EServerConnectionState.AwaitingClient);
                    Dispatcher.RegisterStateMachine(this, m_ServerSM);
                    break;
            }
        }

        public InMemoryConnection NetworkImpl => Network as InMemoryConnection;
        public override Enum State => m_ClientSM != null ? m_ClientSM.State : m_ServerSM.State;

        public Enum StateImpl
        {
            set
            {
                m_ClientSM?.SetState(value);
                m_ServerSM?.SetState(value);
            }
        }

        public override void Disconnect(EDisconnectReason eReason)
        {
            throw new NotImplementedException();
        }
    }
}
