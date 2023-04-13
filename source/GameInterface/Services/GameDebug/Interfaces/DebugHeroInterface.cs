using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using Serilog;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.GameDebug.Interfaces
{
    internal interface IDebugHeroInterface : IGameAbstraction
    {
        void ResolveHero(ResolveDebugHero message);
    }

    public class DebugHeroInterface : IDebugHeroInterface
    {
        private static readonly ILogger Logger = LogManager.GetLogger<HeroInterface>();

        public static readonly string Player1_Id = "Player 1";
        public static readonly string Player2_Id = "Player 2";

        private readonly IMessageBroker messageBroker;

        public DebugHeroInterface(IMessageBroker messageBroker)
        {
            this.messageBroker = messageBroker;
        }

        public void ResolveHero(ResolveDebugHero message)
        {
            messageBroker.Publish(this, new HeroResolved(message.TransactionID, message.PlayerId));
        }
    }
}
