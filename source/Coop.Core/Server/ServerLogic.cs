using Common.Messaging;
using Coop.Core.Server.States;
using GameInterface;

namespace Coop.Core.Server
{
    public class ServerLogic : IServerLogic
    {
        public IServerState State
        {
            get { return _state; }
            set
            {
                _state?.Dispose();
                _state = value;
            }
        }
        private IServerState _state;

        private readonly IGameInterface gameInterface;

        public IMessageBroker MessageBroker { get; }

        public ICoopServer NetworkServer { get; }

        public ServerLogic(IMessageBroker messageBroker, ICoopServer networkServer, IGameInterface gameInterface)
        {
            State = new InitialServerState(this, messageBroker);
            MessageBroker = messageBroker;
            NetworkServer = networkServer;
            this.gameInterface = gameInterface;
        }

        public void Start()
        {
            State.Start();
        }

        public void Stop()
        {
            State.Stop();
            NetworkServer.Stop();
        }

        public void Dispose()
        {
            State.Dispose();
        }
    }
}
