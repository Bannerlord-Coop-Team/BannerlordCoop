using Common.Messaging;
using Coop.Core.Server.Connections.Messages.Incoming;
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
        public CharacterCreationState(IClientLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
        {
            MessageBroker.Subscribe<CharacterCreationFinished>(Handle);
            MessageBroker.Subscribe<NewHeroPackaged>(Handle);
            MessageBroker.Subscribe<MainMenuEntered>(Handle);
        }

        

        public override void Dispose()
        {
            MessageBroker.Unsubscribe<NewHeroPackaged>(Handle);
            MessageBroker.Unsubscribe<MainMenuEntered>(Handle);
        }

        private void Handle(MessagePayload<MainMenuEntered> obj)
        {
            Logic.State = new MainMenuState(Logic, MessageBroker);
        }

        private void Handle(MessagePayload<CharacterCreationFinished> obj)
        {
            MessageBroker.Publish(this, new PackageMainHero());
        }

        private void Handle(MessagePayload<NewHeroPackaged> obj)
        {
            INetworkEvent networkEvent = new NetworkTransferedHero(obj.What.Package);
            Logic.NetworkMessageBroker.PublishNetworkEvent(networkEvent);
        }

        public override void EnterMainMenu()
        {
            MessageBroker.Publish(this, new EnterMainMenu());
        }

        public override void Connect()
        {
        }

        public override void Disconnect()
        {
            MessageBroker.Publish(this, new EnterMainMenu());
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
