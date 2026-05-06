using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Serialization;
using GameInterface.Services.ItemObjects.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Smithing.Handlers
{
    internal class CraftingCampaignBehaviorTickHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehaviorTickHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public CraftingCampaignBehaviorTickHandler(
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<HourTicked>(Handle);
            messageBroker.Subscribe<NetworkHourlyTickServer>(Handle);
            messageBroker.Subscribe<NetworkHourlyTickClients>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<HourTicked>(Handle);
            messageBroker.Unsubscribe<NetworkHourlyTickServer>(Handle);
            messageBroker.Unsubscribe<NetworkHourlyTickClients>(Handle);
        }

        private void Handle(MessagePayload<HourTicked> obj)
        {
            SendHourlyTick(obj.What);
        }

        private void Handle(MessagePayload<NetworkHourlyTickServer> obj)
        {
            NetworkHourlyTickClients message = new(obj.What);
            network.SendAll(message);

            HourlyTick(message);
        }

        private void Handle(MessagePayload<NetworkHourlyTickClients> obj)
        {
            HourlyTick(obj.What);
        }

        private void SendHourlyTick(HourTicked obj)
        {
            if (!objectManager.TryGetId(obj.CraftingCampaignBehavior, out var craftingCampaignBehaviorId))
            {
                Logger.Error("Unable to get network ID for Behavior instance of type {type}", obj.CraftingCampaignBehavior?.GetType());
                return;
            }
            network.SendAll(new NetworkHourlyTickServer(craftingCampaignBehaviorId));
        }

        private void HourlyTick(NetworkHourlyTickClients obj)
        {
            if (!objectManager.TryGetObject(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior))
            {
                Logger.Error("Unable to get object for craftingCampaignBehaviorId {id}", obj.CraftingCampaignBehaviorId);
                return;
            }

            // Replace TaleWorlds implementation
            foreach (KeyValuePair<Hero, CraftingCampaignBehavior.HeroCraftingRecord> keyValuePair in craftingCampaignBehavior._heroCraftingRecords)
            {
                if (keyValuePair.Key.CurrentSettlement != null)
                {
                    int maxHeroCraftingStamina = craftingCampaignBehavior.GetMaxHeroCraftingStamina(keyValuePair.Key);
                    if (keyValuePair.Value.CraftingStamina < maxHeroCraftingStamina)
                    {
                        keyValuePair.Value.CraftingStamina = MathF.Min(maxHeroCraftingStamina, keyValuePair.Value.CraftingStamina + CraftingCampaignBehavior.GetStaminaHourlyRecoveryRate(keyValuePair.Key));
                    }
                }
            }
        }
    }
}
