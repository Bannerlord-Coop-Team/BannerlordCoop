using Common.Messaging;
using Coop.Core.Client.States;
using Coop.Core.Configuration;
using Coop.Core.Debugging.Logger;
using System.Configuration;

namespace Coop.Core.Client
{
    public class ClientLogic : IClientLogic
    {
        public ILogger Logger { get; }
        public ICoopClient NetworkClient { get; }
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

        public ClientLogic(
            ILogger logger,
            ICoopClient networkClient, 
            IMessageBroker messageBroker)
        {
            Logger = logger;
            NetworkClient = networkClient;
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
