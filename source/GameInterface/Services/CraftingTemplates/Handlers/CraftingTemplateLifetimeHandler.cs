using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.BasicCultureObjects.Handlers;
using GameInterface.Services.BasicCultureObjects.Messages;
using GameInterface.Services.CraftingTemplates.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;

namespace GameInterface.Services.CraftingTemplates.Handlers
{
    public class CraftingTemplateLifetimeHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<CraftingTemplateLifetimeHandler>();

        public CraftingTemplateLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<CraftingTemplateCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateCraftingTemplate>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<CraftingTemplateCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateCraftingTemplate>(Handle);
        }

        private void Handle(MessagePayload<CraftingTemplateCreated> payload)
        {
            objectManager.AddNewObject(payload.What.CraftingTemplate, out string newId);
            NetworkCreateCraftingTemplate message = new(newId);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkCreateCraftingTemplate> obj)
        {
            var newCultureObject = ObjectHelper.SkipConstructor<BasicCultureObject>();

            var payload = obj.What;

            objectManager.AddExisting(payload.CraftingTemplateId, newCultureObject);
        }
    }
}