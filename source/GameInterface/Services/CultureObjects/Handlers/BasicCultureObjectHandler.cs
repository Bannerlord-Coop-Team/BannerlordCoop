using Common.Logging;
using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.CraftingService.Handlers;
using GameInterface.Services.CraftingService.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;
using Common.Util;
using TaleWorlds.Localization;
using GameInterface.Services.BasicCultureObjects.Messages;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BasicCultureObjects.Handlers
{
    internal class BasicCultureObjectHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<BasicCultureObjectHandler>();

        public BasicCultureObjectHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<BasicCultureObjectCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateBasicCultureObject>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<BasicCultureObjectCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateBasicCultureObject>(Handle);
        }

        private void Handle(MessagePayload<BasicCultureObjectCreated> payload)
        {
            objectManager.AddNewObject(payload.What.CultureObject, out string newId);
            NetworkCreateBasicCultureObject message = new(newId);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkCreateBasicCultureObject> obj)
        {
            var newCultureObject = ObjectHelper.SkipConstructor<BasicCultureObject>();

            var payload = obj.What;

            objectManager.AddExisting(payload.CultureObjectId, newCultureObject);
        }
    }
}
