using Common.Messaging;
using Coop.Core.Client.States;
using NLog;

namespace Coop.Core.Client
{
    public class ClientLogic : IClientLogic
    {
        public ILogger Logger { get; }
        public IMessageBroker MessageBroker { get; }
        public IClientState State 
        {
            get { return _state; }
            set 
            {
                _state?.Dispose();
                _state = value;
            } 
        }
        private IClientState _state;

        public ClientLogic(ILogger logger, IMessageBroker messageBroker)
        {
            Logger = logger;
            MessageBroker = messageBroker;
            State = new MainMenuState(this, messageBroker);
        }

        public void Start()
        {
            Connect();
        }

        public void Stop()
        {
            Disconnect();
        }

        public void Dispose()
        {
            State.Dispose();
        }

        public void Connect()
        {
            State.Connect();
        }

        public void Disconnect()
        {
            State.Disconnect();
        }

        public void StartCharacterCreation()
        {
            State.StartCharacterCreation();
        }

        public void LoadSavedData()
        {
            State.LoadSavedData();
        }

        public void ExitGame()
        {
            State.ExitGame();
        }

        public void EnterMainMenu()
        {
            State.EnterMainMenu();
        }

        public void EnterCampaignState()
        {
            State.ExitGame();
        }

        public void EnterMissionState()
        {
            State.EnterMainMenu();
        }


    }
}
