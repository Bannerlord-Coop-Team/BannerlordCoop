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

        private readonly ICoopServer networkServer;
        private readonly IGameInterface gameInterface;

        public IMessageBroker MessageBroker { get; }

        public ServerLogic(IMessageBroker messageBroker, ICoopServer networkServer, IGameInterface gameInterface)
        {
            State = new InitialServerState(this, messageBroker);
            MessageBroker = messageBroker;
            this.networkServer = networkServer;
            this.gameInterface = gameInterface;
        }

        public void Start()
        {
            networkServer.Start();

            State.Start();
        }

        public void Stop()
        {
            networkServer.Stop();
        }

        public void Dispose()
        {
            State.Dispose();
        }
    }
}
