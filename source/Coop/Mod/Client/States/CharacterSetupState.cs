using Common.Messages;
using Coop.Mod.LogicStates.Client;
using GameInterface.Messages.Commands;
using GameInterface.Messages.Events;
using System;

namespace Coop.Mod.Client.States
{
    internal class CharacterSetupState : ClientStateBase
    {
        public CharacterSetupState(IClientLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
        {
            messageBroker.Subscribe<CharacterCreationFinishedEvent>(HandleCharacterCreationFinished);
            messageBroker.Subscribe<SerializePlayerHeroResponse>(HandleSerializePlayerHeroResponse);

        }
        
        private void HandleCharacterCreationFinished(MessagePayload<CharacterCreationFinishedEvent> payload)
        {
            MessageBroker.Publish(this, new SerializePlayerHeroCommand());
        }

        private void HandleSerializePlayerHeroResponse(MessagePayload<SerializePlayerHeroResponse> obj)
        {
            throw new NotImplementedException();
        }
    }
}
