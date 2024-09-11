using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.CraftingService.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.CraftingService.Handlers
{
    /// <summary>
    /// Handles all changes to clans on client.
    /// </summary>
    public class CraftingHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<CraftingHandler>();

        public CraftingHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<CraftingCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateCrafting>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<CraftingCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateCrafting>(Handle);
        }

        private void Handle(MessagePayload<CraftingCreated> payload)
        {
            NetworkCreateCrafting message = new(payload.What.Data);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkCreateCrafting> obj)
        {
            var payload = obj.What.Data;

            if (objectManager.TryGetObject(payload.CraftingTemplateId, out CraftingTemplate template) == false) return;
            if (objectManager.TryGetObject(payload.CultureId, out BasicCultureObject cultureObj) == false) return;

            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    var crafting = new Crafting(template, cultureObj, new TextObject(payload.Name));
                    objectManager.AddExisting(payload.CraftingId, crafting);
                }
            });
        }
    }
}
