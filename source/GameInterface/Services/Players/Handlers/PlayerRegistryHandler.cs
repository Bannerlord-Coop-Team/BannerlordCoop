using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Players.Messages;
using Serilog;
using System;

namespace GameInterface.Services.Players.Handlers
{
    internal class PlayerRegistryHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<PlayerRegistryHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IPlayerRegistry playerRegistry;

        public PlayerRegistryHandler(IMessageBroker messageBroker, IPlayerRegistry playerRegistry)
        {
            this.messageBroker = messageBroker;
            this.playerRegistry = playerRegistry;
            this.messageBroker.Subscribe<NewPlayerHeroRegistered>(Handle);
            this.messageBroker.Subscribe<RegisterPlayer>(Handle);
        }

        private void Handle(MessagePayload<NewPlayerHeroRegistered> obj)
        {
            var player = obj.What.Player;

            if(!playerRegistry.AddPlayer(player))
            {
                Logger.Error("Player has been already added.");
            } 

        }

        private void Handle(MessagePayload<RegisterPlayer> obj)
        {
            var player = obj.What.Player;

            if (!playerRegistry.AddPlayer(player))
            {
                Logger.Error("Player has been already added.");
            }
        }


        public void Dispose()
        {
            messageBroker.Unsubscribe<NewPlayerHeroRegistered>(Handle);
            messageBroker.Unsubscribe<RegisterPlayer>(Handle);
        }
    }
}
