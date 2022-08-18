using Common.LogicStates;
using Common.Messaging;
using Coop.Mod.Client.States;
using Coop.Mod.LogicStates.Client;
using GameInterface.Services.GameState.Messages;
using NLog;

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
            State = new MainMenuState(this, messageBroker);
        }

        public void GoToMainMenu()
        {
            State = new MainMenuState(this, MessageBroker);
        }

        public void Connect()
        {
            throw new System.NotImplementedException();
        }

        public void Disconnect()
        {
            throw new System.NotImplementedException();
        }

        public void StartCharacterCreation()
        {
            MessageBroker.Publish(this, new StartCreateCharacter());
            State = new CharacterCreationState(this, MessageBroker);
        }

        public void LoadSavedData()
        {
            throw new System.NotImplementedException();
        }

        public void ExitGame()
        {
            throw new System.NotImplementedException();
        }

        public void EnterMainMenu()
        {
            throw new System.NotImplementedException();
        }
    }
}
