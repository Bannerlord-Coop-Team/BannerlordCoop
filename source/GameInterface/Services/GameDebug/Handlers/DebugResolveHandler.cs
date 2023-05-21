using Common.Messaging;
using GameInterface.Services.GameDebug.Interfaces;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Messages;
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

        public void Dispose()
        {
            messageBroker.Unsubscribe<ResolveDebugHero>(Handle);
        }

        private void Handle(MessagePayload<ResolveDebugHero> obj)
        {
            heroDebugInterface.TryResolveHero(obj.What, out string stringID);
            messageBroker.Respond(obj.Who, new HeroResolved(stringID));
        }
    }
}
