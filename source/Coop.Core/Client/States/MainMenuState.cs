using Common.Messaging;
using Coop.Core.Client.Messages;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.MobileParties.Messages;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// State Logic Controller for the Main Menu Client State
    /// </summary>
    public class MainMenuState : ClientStateBase
    {
        public MainMenuState(IClientLogic logic) : base(logic)
        {
            Logic.MessageBroker.Subscribe<NetworkConnected>(Handle_NetworkConnected);
        }

        public override void Dispose() 
        {
            Logic.MessageBroker.Unsubscribe<NetworkConnected>(Handle_NetworkConnected);
        }

        public override void Connect()
        {
            Logic.Network.Start();
        }

        internal void Handle_NetworkConnected(MessagePayload<NetworkConnected> obj)
        {
            Logic.ValidateModules();
        }

        public override void Disconnect()
        {
            Logic.MessageBroker.Publish(this, new EnterMainMenu());
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

        public override void ValidateModules()
        {
            Logic.State = new ValidateModuleState(Logic);
        }
    }
}
