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

        private static Guid Hero1_Id = Guid.Parse("ed122845-8743-449f-bc3c-4adf6fd9060c");
        private static Guid Hero2_Id = Guid.Empty; // TODO

        private static Dictionary<string, Guid> Player_To_HeroID = new Dictionary<string, Guid>
        {
            { Player1_Id, Hero1_Id },
            { Player2_Id, Hero2_Id },
        };

        private readonly IMessageBroker messageBroker;

        public DebugHeroInterface(IMessageBroker messageBroker)
        {
            this.messageBroker = messageBroker;
        }

        public void ResolveHero(ResolveDebugHero message)
        { 
            if(Player_To_HeroID.TryGetValue(message.PlayerId, out Guid heroId))
            {
                messageBroker.Publish(this, new HeroResolved(message.TransactionID, heroId));
            }
            else
            {
                Logger.Error("Could not resolve debug hero with id: {id}", message.PlayerId);
                messageBroker.Publish(this, new ResolveHeroNotFound(message.TransactionID));  
            }
        }
    }
}
