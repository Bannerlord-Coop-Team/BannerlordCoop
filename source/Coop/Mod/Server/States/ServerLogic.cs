using Common.LogicStates;
using Common.Messages;
using Coop.Mod.LogicStates.Server;

namespace Coop.Mod.Server.States
{
    public class ServerLogic : IServerLogic
    {
        public IState State { get => _state; set => _state = (IServerState)value; }
        private IServerState _state;
        public IMessageBroker MessageBroker { get; }

        public ServerLogic(IMessageBroker messageBroker)
        {
            _state = new InitialServerState(this);
            MessageBroker = messageBroker;
        }
    }
}
