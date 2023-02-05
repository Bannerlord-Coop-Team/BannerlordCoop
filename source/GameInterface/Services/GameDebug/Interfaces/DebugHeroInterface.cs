using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using Serilog;
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

        private static uint Hero1_Id = 0; // TODO
        private static uint Hero2_Id = 0; // TODO

        private static Dictionary<string, uint> Player_To_HeroID = new Dictionary<string, uint>
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
            if(Player_To_HeroID.TryGetValue(message.PlayerId, out uint heroId) && false)
            {
                messageBroker.Publish(this, new HeroResolved(message.TransactionId, heroId));
            }
            else
            {
                Logger.Error("Could not resolve debug hero with id: {id}", message.PlayerId);
                messageBroker.Publish(this, new ResolveHeroNotFound(message.TransactionId));  
            }
        }
    }
}
