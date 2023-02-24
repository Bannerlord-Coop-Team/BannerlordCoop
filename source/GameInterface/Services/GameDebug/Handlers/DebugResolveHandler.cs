using Common.Messaging;
using GameInterface.Services.GameDebug.Interfaces;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.UI.Messages;
using System;

namespace GameInterface.Services.GameDebug.Handlers
{
    internal class DebugResolveHandler : IHandler
    {
        private readonly IDebugHeroInterface heroDebugInterface;
        private readonly IMessageBroker messageBroker;

        public DebugResolveHandler(IDebugHeroInterface heroDebugInterface, IMessageBroker messageBroker)
        {
            this.heroDebugInterface = heroDebugInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<ResolveDebugHero>(Handle);
        }

        private void Handle(MessagePayload<ResolveDebugHero> obj)
        {
            heroDebugInterface.ResolveHero(obj.What);
        }
    }
}
