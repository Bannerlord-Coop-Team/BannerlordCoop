using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.CraftingTemplates.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
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
            if(objectManager.AddNewObject(payload.What.CraftingTemplate, out string newId) == false)
            {
                Logger.Error("Failed to add {type} to manager", typeof(CraftingTemplate));
                return;
            }
            NetworkCreateCraftingTemplate message = new(newId);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkCreateCraftingTemplate> obj)
        {
            var newCraftingTemplate = ObjectHelper.SkipConstructor<CraftingTemplate>();

            var payload = obj.What;

            if(objectManager.AddExisting(payload.CraftingTemplateId, newCraftingTemplate) == false)
            {
                Logger.Error("Failed to add {type} to manager with id {id}", typeof(CraftingTemplate), payload.CraftingTemplateId);
                return;
            }
        }
    }
}