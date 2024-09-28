using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.CraftingService.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.CraftingService.Handlers
{
    /// <summary>
    /// Handles all changes to clans on client.
    /// </summary>
    public class CraftingLifetimeHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<CraftingLifetimeHandler>();

        public CraftingLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<CraftingCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateCrafting>(Handle);
            messageBroker.Subscribe<CraftingRemoved>(Handle);
            messageBroker.Subscribe<NetworkRemoveCrafting>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<CraftingCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateCrafting>(Handle);
            messageBroker.Unsubscribe<CraftingRemoved>(Handle);
            messageBroker.Unsubscribe<NetworkRemoveCrafting>(Handle);
        }

        private void Handle(MessagePayload<CraftingCreated> payload)
        {
            objectManager.AddNewObject(payload.What.Crafting, out string newCraftingId);

            CraftingCreatedData data = new(newCraftingId, 
                payload.What.CraftingTemplate.StringId, 
                payload.What.CultureObject?.StringId, 
                payload.What.TextObject.Value);

            NetworkCreateCrafting message = new(data);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkCreateCrafting> obj)
        {
            var payload = obj.What.Data;

            if (objectManager.TryGetObject(payload.CraftingTemplateId, out CraftingTemplate template) == false)
            {
                Logger.Error("Failed to get object for {type} with id {id}", typeof(CraftingTemplate), payload.CraftingTemplateId);
                return;
            }
            if (objectManager.TryGetObject(payload.CultureId, out CultureObject cultureObj) == false)
            {
                Logger.Error("Failed to get object for {type} with id {id}", typeof(CultureObject), payload.CultureId);
                return;
            }

            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    var crafting = new Crafting(template, cultureObj, new TextObject(payload.Name));
                    objectManager.AddExisting(payload.CraftingId, crafting);
                }
            });
        }

        private void Handle(MessagePayload<CraftingRemoved> payload)
        {
            if(objectManager.TryGetId(payload.What.crafting, out string craftingId) == false)
            {
                Logger.Error("Failed to get ID for {type}", typeof(Crafting));
                return;
            }
            if(objectManager.Remove(payload.What.crafting) == false)
            {
                Logger.Error("Failed to remove {type}", typeof(Crafting));
                return;
            }
            NetworkRemoveCrafting message = new(craftingId);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkRemoveCrafting> obj)
        {
            var payload = obj.What;

            if (objectManager.TryGetObject(payload.CraftingId, out Crafting crafting) == false)
            {
                Logger.Error("Failed to get object for {type} with id {id}", typeof(Crafting), payload.CraftingId);
                return;
            }

            objectManager.Remove(crafting);
        }
    }
}
