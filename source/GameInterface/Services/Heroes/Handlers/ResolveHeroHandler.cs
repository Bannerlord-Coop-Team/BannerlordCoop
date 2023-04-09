using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using Serilog;

namespace GameInterface.Services.Heroes.Handlers
{
    internal class ResolveHeroHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<NewHeroHandler>();

        private readonly IHeroInterface heroInterface;
        private readonly IMessageBroker messageBroker;

        public ResolveHeroHandler(
            IHeroInterface heroInterface,
            IMessageBroker messageBroker)
        {
            this.heroInterface = heroInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<ResolveHero>(Handle);
        }

        private void Handle(MessagePayload<ResolveHero> obj)
        {
            heroInterface.ResolveHero(obj.What);
        }
    }
}
