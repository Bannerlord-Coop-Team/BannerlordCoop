using Common.Messaging;
using Common.Util;
using Coop.Core.Client.Messages;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameDebug.Interfaces;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Modules.Messages;
using System.Threading.Tasks;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// State Logic Controller for the Validate Module Client State
    /// </summary>
    public class ValidateModuleState : ClientStateBase
    {
        public ValidateModuleState(IClientLogic logic) : base(logic)
        {
            Logic.NetworkMessageBroker.Subscribe<MainMenuEntered>(Handle);
            Logic.NetworkMessageBroker.Subscribe<CharacterCreationStarted>(Handle);
            Logic.NetworkMessageBroker.Subscribe<NetworkClientValidated>(Handle);

            Logic.NetworkMessageBroker.PublishNetworkEvent(new NetworkClientValidate(DebugHeroInterface.Player1_Id));
        }

        public override void Dispose()
        {
            Logic.NetworkMessageBroker.Unsubscribe<MainMenuEntered>(Handle);
            Logic.NetworkMessageBroker.Unsubscribe<CharacterCreationStarted>(Handle);
            Logic.NetworkMessageBroker.Unsubscribe<NetworkClientValidated>(Handle);
        }

        private void Handle(MessagePayload<NetworkClientValidated> obj)
        {
            if (obj.What.HeroExists)
            {
                Logic.ControlledHeroId = obj.What.HeroId;
                Logic.LoadSavedData();
            }
            else
            {
                Logic.StartCharacterCreation();   
            }
        }

        public override void EnterMainMenu()
        {
            Logic.NetworkMessageBroker.Publish(this, new EnterMainMenu());
        }

        private void Handle(MessagePayload<MainMenuEntered> obj)
        {
            Logic.State = new MainMenuState(Logic);
        }

        public override void LoadSavedData()
        {
#if DEBUG
            Logic.NetworkMessageBroker.Publish(this, new LoadDebugGame());
            Logic.State = new LoadingState(Logic);
#else
            Logic.State = new ReceivingSavedDataState(Logic);
#endif
        }

        public override void Connect()
        {
        }

        public override void Disconnect()
        {
            Logic.NetworkMessageBroker.Publish(this, new EnterMainMenu());
        }

        public override void EnterCampaignState()
        {
        }

        public override void EnterMissionState()
        {
        }

        public override void ExitGame()
        {
        }

        public override void StartCharacterCreation()
        {
            Logic.NetworkMessageBroker.Publish(this, new StartCharacterCreation());
        }

        private void Handle(MessagePayload<CharacterCreationStarted> obj)
        {
            Logic.State = new CharacterCreationState(Logic);
        }

        public override void ValidateModules()
        {
        }
    }
}
