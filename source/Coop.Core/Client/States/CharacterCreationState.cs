using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Packets;
using Coop.Core.Server.Connections.Messages;
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
            INetworkEvent networkEvent = new PlayerTransferCharacter(obj.What.Package);
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
