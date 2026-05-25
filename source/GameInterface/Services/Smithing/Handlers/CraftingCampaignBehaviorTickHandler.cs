using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Smithing.Messages;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Smithing.Handlers
{
    internal class CraftingCampaignBehaviorTickHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehaviorTickHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly IPlayerRegistry playerRegistry;

        public CraftingCampaignBehaviorTickHandler(
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            INetwork network,
            IPlayerRegistry playerRegistry)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            this.playerRegistry = playerRegistry;
            messageBroker.Subscribe<HourTicked>(Handle);
            messageBroker.Subscribe<NetworkHourlyTick>(Handle);
            messageBroker.Subscribe<DailySettlementTick>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<HourTicked>(Handle);
            messageBroker.Unsubscribe<NetworkHourlyTick>(Handle);
            messageBroker.Unsubscribe<DailySettlementTick>(Handle);
        }

        private void Handle(MessagePayload<HourTicked> obj)
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.CraftingCampaignBehavior, out var craftingCampaignBehaviorId)) return;

            network.SendAll(new NetworkHourlyTick(craftingCampaignBehaviorId));

            // Needed because crafting stamina recovers as time passes while a client is in the crafting menu (unlike vanilla)
            network.SendAll(new NetworkRefreshCraftingVM());
        }

        private void Handle(MessagePayload<NetworkHourlyTick> obj)
        {
            HourlyTick(obj.What);
        }

        private void Handle(MessagePayload<DailySettlementTick> obj)
        {
            DailyTickSettlement(obj.What);
        }

        private void HourlyTick(NetworkHourlyTick obj)
        {
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior)) return;

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

        private void DailyTickSettlement(DailySettlementTick obj)
        {
            if (obj.Settlement.IsTown && obj.CraftingCampaignBehavior.CraftingOrders[obj.Settlement.Town].IsThereAvailableSlot())
            {
                List<Hero> list = new List<Hero>(obj.Settlement.HeroesWithoutParty);
                foreach (MobileParty mobileParty in obj.Settlement.Parties)
                {
                    // Prevents adding town orders with player hero order owners
                    if (mobileParty.LeaderHero != null && !mobileParty.IsMainParty && !playerRegistry.Contains(mobileParty))
                    {
                        list.Add(mobileParty.LeaderHero);
                    }
                }
                foreach (Hero hero in list)
                {
                    // Prevents adding town orders with player hero order owners
                    if (hero != Hero.MainHero && !playerRegistry.Contains(hero.PartyBelongedTo) && MBRandom.RandomFloat <= 0.05f)
                    {
                        int availableSlot = obj.CraftingCampaignBehavior.CraftingOrders[obj.Settlement.Town].GetAvailableSlot();
                        if (availableSlot <= -1)
                        {
                            break;
                        }
                        obj.CraftingCampaignBehavior.CreateTownOrder(hero, availableSlot);
                    }
                }
            }
        }
    }
}
