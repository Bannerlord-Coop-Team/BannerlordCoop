using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using System;
namespace Coop.Core.Client.States
{
    internal class CharacterCreationState : ClientStateBase
    {
        public CharacterCreationState(IClientLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
        {
            messageBroker.Subscribe<CharacterCreationFinished>(Handle);
        }
        public override void Dispose()
        {
            MessageBroker.Unsubscribe<CharacterCreationFinished>(Handle);
        }

        private void Handle(MessagePayload<CharacterCreationFinished> obj)
        {
            MessageBroker.Publish(this, new EnterMainMenu());
            Logic.State = new ReceivingSavedDataState(Logic, MessageBroker);
        }

        public override void Connect()
        {
        }

        public override void Disconnect()
        {
        }

        public override void EnterMainMenu()
        {
            throw new NotImplementedException();
        }

        public override void ExitGame()
        {
            throw new NotImplementedException();
        }

        public override void LoadSavedData()
        {
            throw new NotImplementedException();
        }

        public override void StartCharacterCreation()
        {
            throw new NotImplementedException();
        }
    }
}
