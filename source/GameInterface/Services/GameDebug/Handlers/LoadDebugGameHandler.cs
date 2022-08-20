using Common.Messaging;
using GameInterface.Services.GameDebug.Interfaces;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            messageBroker.Subscribe<GameLoaded>(Handle);
        }

        private void Handle(MessagePayload<LoadDebugGame> payload)
        {
            gameDebugInterface.LoadDebugGame();
        }

        private void Handle(MessagePayload<GameLoaded> payload)
        {
            messageBroker.Publish(this, new MainMenuEntered());
        }
    }
}
