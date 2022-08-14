using Common.LogicStates;
using Coop.Communication.MessageBroker;
using Coop.Mod.LogicStates;
using Coop.Mod.LogicStates.Client;
using NLog;
using System.Threading.Tasks;

namespace Coop.Mod.Client
{
    public class ClientLogic : IClientLogic, IClientStateBase
    {
        public ILogger Logger { get; }
        public IMessageBroker MessageBroker { get; }
        public IState State { get => _state; set => _state = (IClientStateBase)value; }
        private IClientStateBase _state;
        
        public ClientLogic(ILogger logger, IMessageBroker messageBroker)
        {
            Logger = logger;
            MessageBroker = messageBroker;
            State = new InitialClientState(this, messageBroker);
        }

        public async Task<bool> Connect()
        {
            return await _state.Connect();
        }
    }
}
