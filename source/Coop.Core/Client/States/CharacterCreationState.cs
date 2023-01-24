using Common.Messaging;
using Coop.Core.Client.Packets;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// State controller for the character creation client state
    /// </summary>
    public class CharacterCreationState : ClientStateBase
    {
        public CharacterCreationState(IClientLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
        {
            MessageBroker.Subscribe<CharacterCreatedHeroPackaged>(Handle);
            MessageBroker.Subscribe<MainMenuEntered>(Handle);
        }

        public override void Dispose()
        {
            MessageBroker.Unsubscribe<CharacterCreatedHeroPackaged>(Handle);
            MessageBroker.Unsubscribe<MainMenuEntered>(Handle);
        }

        private void Handle(MessagePayload<MainMenuEntered> obj)
        {
            Logic.State = new MainMenuState(Logic, MessageBroker);
        }

        private void Handle(MessagePayload<CharacterCreatedHeroPackaged> obj)
        {
            NewHeroPacket heroPacket = new NewHeroPacket(obj.What.Package);

            // Send to server
            Logic.NetworkClient.SendAll(heroPacket);
            Logic.State = new ReceivingSavedDataState(Logic, MessageBroker);
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
