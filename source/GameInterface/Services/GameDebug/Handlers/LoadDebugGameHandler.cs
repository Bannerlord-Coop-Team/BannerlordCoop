<<<<<<< HEAD
﻿using Common.Messaging;
using GameInterface.Services.GameDebug.Interfaces;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.UI.Messages;
=======
﻿using Common;
using Common.Messaging;
using GameInterface.Services.GameDebug.Interfaces;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;
>>>>>>> NetworkEvent-refactor

namespace GameInterface.Services.GameDebug.Handlers
{
    internal class LoadDebugGameHandler : IHandler
    {
        private readonly IGameDebugInterface gameDebugInterface;
        private readonly IMessageBroker messageBroker;

        public LoadDebugGameHandler(IGameDebugInterface gameDebugInterface, IMessageBroker messageBroker)
        {
            this.gameDebugInterface = gameDebugInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<LoadDebugGame>(Handle);
<<<<<<< HEAD
=======
            messageBroker.Subscribe<GameLoaded>(Handle);
>>>>>>> NetworkEvent-refactor
        }

        private void Handle(MessagePayload<LoadDebugGame> payload)
        {
<<<<<<< HEAD
            messageBroker.Publish(this, new StartLoadingScreen());
            gameDebugInterface.LoadDebugGame();
        }
=======
            gameDebugInterface.LoadDebugGame();
        }

        private void Handle(MessagePayload<GameLoaded> payload)
        {
            messageBroker.Publish(this, new DebugGameStarted());
        }
>>>>>>> NetworkEvent-refactor
    }
}
