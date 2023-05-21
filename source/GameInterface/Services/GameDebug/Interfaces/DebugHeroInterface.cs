﻿using Common.Logging;
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
        bool TryResolveHero(ResolveDebugHero message, out string stringId);
    }

    public class DebugHeroInterface : IDebugHeroInterface
    {
        private static readonly ILogger Logger = LogManager.GetLogger<HeroInterface>();

        public static readonly string Player1_Id = "Player 1";
        public static readonly string Player2_Id = "Player 2";

        private static Dictionary<string, string> PlayerIdMap = new Dictionary<string, string>
        {
            { Player1_Id, "TransferredHero2862" }
        };

        private readonly IMessageBroker messageBroker;

        public DebugHeroInterface(IMessageBroker messageBroker)
        {
            this.messageBroker = messageBroker;
        }

        public bool TryResolveHero(ResolveDebugHero message, out string stringId)
        {
            if(PlayerIdMap.TryGetValue(message.PlayerId, out stringId) == false)
            {
                Logger.Warning("Could not find {player} in {dict}", message.PlayerId, PlayerIdMap);
                return false;
            }

            return true;
        }
    }
}
