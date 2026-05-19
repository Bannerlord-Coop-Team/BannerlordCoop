using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing.Interfaces;
using GameInterface.Services.Smithing.Messages;
using Serilog;
using System.Collections.Generic;
using System.Xml.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Smithing.Handlers
{
    internal class CraftingCampaignBehaviorWeaponNameHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehaviorWeaponNameHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public CraftingCampaignBehaviorWeaponNameHandler(
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<BehaviorCraftedWeaponNameSet>(Handle);
            messageBroker.Subscribe<NetworkBehaviorSetCraftedWeaponNameServer>(Handle);
            messageBroker.Subscribe<NetworkBehaviorSetCraftedWeaponNameClients>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<BehaviorCraftedWeaponNameSet>(Handle);
            messageBroker.Unsubscribe<NetworkBehaviorSetCraftedWeaponNameServer>(Handle);
            messageBroker.Unsubscribe<NetworkBehaviorSetCraftedWeaponNameClients>(Handle);
        }

        private void Handle(MessagePayload<BehaviorCraftedWeaponNameSet> obj)
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.CraftingCampaignBehavior, out var craftingCampaignBehaviorId)) return;

            // Send to server from client
            NetworkBehaviorSetCraftedWeaponNameServer message = new(
                craftingCampaignBehaviorId,
                obj.What.CraftedWeaponId,
                obj.What.Name.ToString() ?? ""
            );
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkBehaviorSetCraftedWeaponNameServer> obj)
        {
            // Send from server to all clients
            NetworkBehaviorSetCraftedWeaponNameClients nameChange = new(obj.What);
            network.SendAll(nameChange);
            SetCraftedWeaponName(nameChange);
        }

        private void Handle(MessagePayload<NetworkBehaviorSetCraftedWeaponNameClients> obj)
        {
            SetCraftedWeaponName(obj.What);
        }

        private void SetCraftedWeaponName(NetworkBehaviorSetCraftedWeaponNameClients obj)
        {
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior)) return;
            ItemObject mbCraftedWeapon = MBObjectManager.Instance.GetObject<ItemObject>(obj.CraftedWeaponId);

            if (craftingCampaignBehavior._craftedItemDictionary.TryGetValue(mbCraftedWeapon, out CraftingCampaignBehavior.CraftedItemInitializationData craftedItemInitializationData))
            {
                craftingCampaignBehavior._craftedItemDictionary[mbCraftedWeapon] = new CraftingCampaignBehavior.CraftedItemInitializationData(
                    craftedItemInitializationData.CraftedData,
                    new TextObject(obj.StringName),
                    craftedItemInitializationData.Culture);
            }
        }
    }
}
