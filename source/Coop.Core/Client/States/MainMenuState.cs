using Common.Messaging;
using Coop.Core.Client.Messages;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// State Logic Controller for the Main Menu Client State
    /// </summary>
    public class MainMenuState : ClientStateBase
    {
        public MainMenuState(IClientLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
        {
            MessageBroker.Subscribe<NetworkConnected>(Handle);
        }

        public override void Dispose()
        {
            MessageBroker.Unsubscribe<NetworkConnected>(Handle);
        }

        public override void Connect()
        {
            Logic.NetworkClient.Start();
        }

        private void Handle(MessagePayload<NetworkConnected> obj)
        {
            if (obj.What.ClientPartyExists)
            {
                Logic.State = new ReceivingSavedDataState(Logic, MessageBroker);
                MessageBroker.Publish(this, new LoadGameSave());
            }
            else
            {
                Logic.State = new CharacterCreationState(Logic, MessageBroker);
                MessageBroker.Publish(this, new StartCreateCharacter());
            }
        }

        public override void Disconnect()
        {
        }

        public override void EnterMainMenu()
        {
        }

        public override void ExitGame()
        {
        }

        public override void LoadSavedData()
        {
        }

        public override void StartCharacterCreation()
        {
        }

        public override void EnterCampaignState()
        {
        }

        public override void EnterMissionState()
        {
        }

        public override void ResolveNetworkGuids()
        {
        }
    }
}
