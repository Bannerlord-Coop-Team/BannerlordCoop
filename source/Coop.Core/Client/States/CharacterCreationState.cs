using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Messages;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// State controller for the character creation client state
    /// </summary>
    public class CharacterCreationState : ClientStateBase
    {
        public CharacterCreationState(IClientLogic logic) : base(logic)
        {
            Logic.NetworkMessageBroker.Subscribe<NewHeroPackaged>(Handle);
            Logic.NetworkMessageBroker.Subscribe<CharacterCreationFinished>(Handle);
            Logic.NetworkMessageBroker.Subscribe<MainMenuEntered>(Handle);
        }

        public override void Dispose()
        {
            Logic.NetworkMessageBroker.Unsubscribe<NewHeroPackaged>(Handle);
            Logic.NetworkMessageBroker.Unsubscribe<CharacterCreationFinished>(Handle);
            Logic.NetworkMessageBroker.Unsubscribe<MainMenuEntered>(Handle);
        }

        private void Handle(MessagePayload<NewHeroPackaged> obj)
        {
            INetworkEvent networkEvent = new NetworkTransferedHero(obj.What.Package);
            Logic.NetworkMessageBroker.PublishNetworkEvent(networkEvent);

            Logic.LoadSavedData();
        }

        private void Handle(MessagePayload<CharacterCreationFinished> obj)
        {
            Logic.NetworkMessageBroker.Publish(this, new PackageMainHero());
        }

        public override void EnterMainMenu()
        {
            Logic.NetworkMessageBroker.Publish(this, new EnterMainMenu());
        }

        private void Handle(MessagePayload<MainMenuEntered> obj)
        {
            Logic.State = new MainMenuState(Logic);
        }

        public override void Connect()
        {
        }

        public override void Disconnect()
        {
            Logic.NetworkMessageBroker.Publish(this, new EnterMainMenu());
        }

        public override void ExitGame()
        {
        }

        public override void LoadSavedData()
        {
            Logic.State = new ReceivingSavedDataState(Logic);
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
        }
    }
}
