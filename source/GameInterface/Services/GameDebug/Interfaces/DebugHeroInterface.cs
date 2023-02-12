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

        private static string Hero1_Id = "TransferredHero2862"; // TODO
        private static string Hero2_Id = string.Empty; // TODO

        private static Dictionary<string, string> Player_To_HeroID = new Dictionary<string, string>
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
            if(Player_To_HeroID.TryGetValue(message.PlayerId, out string heroStringId))
            {
                messageBroker.Publish(this, new HeroResolved(message.TransactionId, heroStringId));
            }
            else
            {
                Logger.Error("Could not resolve debug hero with id: {id}", message.PlayerId);
                messageBroker.Publish(this, new ResolveHeroNotFound(message.TransactionId));  
            }
        }
    }
}
