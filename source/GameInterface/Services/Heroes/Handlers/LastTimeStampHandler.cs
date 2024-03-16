using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Handlers
{
    /// <summary>
    /// Handler for LastTimeStampForActivity
    /// </summary>
    public class LastTimeStampHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<LastTimeStampHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;

        public LastTimeStampHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;

            messageBroker.Subscribe<ChangeLastTimeStamp>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ChangeLastTimeStamp>(Handle);
        }

        private void Handle(MessagePayload<ChangeLastTimeStamp> payload)
        {
            var data = payload.What;
            if(objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
            }

            instance.LastTimeStampForActivity = data.LastTimeStamp;
        }
    }
}