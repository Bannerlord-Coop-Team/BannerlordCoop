<<<<<<< HEAD
﻿using Common.Messaging;
=======
﻿using Common;
using Common.Messaging;
>>>>>>> NetworkEvent-refactor
using GameInterface.Services.GameDebug.Interfaces;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;

namespace GameInterface.Services.GameDebug.Handlers
{
<<<<<<< HEAD
    internal class SkipCharacterCreationHandler : IHandler
    {
        private readonly IDebugCharacterCreationInterface characterCreationInterface;
        private readonly IMessageBroker messageBroker;

        public SkipCharacterCreationHandler(IDebugCharacterCreationInterface characterCreationInterface, IMessageBroker messageBroker)
=======
    internal class SkipCharacterCreationHandler
    {
        private readonly ICharacterCreationInterface characterCreationInterface;
        private readonly IMessageBroker messageBroker;

        public SkipCharacterCreationHandler(ICharacterCreationInterface characterCreationInterface, IMessageBroker messageBroker)
>>>>>>> NetworkEvent-refactor
        {
            this.characterCreationInterface = characterCreationInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<CharacterCreationStarted>(Handle);
<<<<<<< HEAD
=======
            messageBroker.Subscribe<GameLoaded>(Handle);
        }

        private void Handle(MessagePayload<GameLoaded> obj)
        {
            messageBroker.Publish(this, new CharacterCreationFinished());
>>>>>>> NetworkEvent-refactor
        }

        private void Handle(MessagePayload<CharacterCreationStarted> obj)
        {
            characterCreationInterface.SkipCharacterCreation();
        }
    }
}
