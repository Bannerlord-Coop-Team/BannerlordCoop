using Common.LogicStates;
using Coop.Communication.MessageBroker;
using Coop.Mod.Client.States;
using Coop.Mod.LogicStates.Client;
using GameInterface;
using NLog;
using System.Threading.Tasks;

namespace Coop.Mod.Client
{
    public class ClientLogic : ClientState, IClientLogic
    {
        public ILogger Logger { get; }
        public IMessageBroker MessageBroker { get; }
        public IGameInterface GameInterface { get; }
        public IState State { get => _state; set => _state = (IClientStateBase)value; }
        private IClientStateBase _state;
        
        public ClientLogic(ILogger logger, IMessageBroker messageBroker, IGameInterface gameInterface)
        {
            Logger = logger;
            MessageBroker = messageBroker;
            GameInterface = gameInterface;
            State = new InitialClientState(this, messageBroker, gameInterface);
        }

        public async Task<bool> Connect()
        {
            return await _state.Connect();
        }

        public async void GoToMainMenu()
        {
            await 
        }
    }
}
