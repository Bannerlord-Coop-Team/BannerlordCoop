using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Towns.Messages;
using GameInterface.Services.Towns.Patches;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Towns.Handlers
{
    /// <summary>
    /// Handles Town Auditor (send all sync).
    /// </summary>
    public class TownAuditorHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<TownAuditorHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;

        public TownAuditorHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
        }

        public void Dispose()
        {
        }
    }
}