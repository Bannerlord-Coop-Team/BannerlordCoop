using Common.LogicStates;
using Common.Messages;
using Coop.Mod.LogicStates;
using Coop.Mod.LogicStates.Client;
using NLog;
using System.Threading.Tasks;

namespace Coop.Mod.Client
{
    public class ClientLogic : IClientLogic, IClientState
    {
        public ILogger Logger { get; }
        public IMessageBroker MessageBroker { get; }
        public IState State { get => _state; set => _state = (IClientState)value; }
        private IClientState _state;
        
        public ClientLogic(ILogger logger, IMessageBroker messageBroker)
        {
            Logger = logger;
            MessageBroker = messageBroker;
            State = new InitialClientState(this, messageBroker);
        }

        public void GoToMainMenu()
        {
            throw new System.NotImplementedException();
        }
    }
}
