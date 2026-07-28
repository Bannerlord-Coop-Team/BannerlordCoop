using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing.Interfaces;
using GameInterface.Services.Smithing.Messages;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.CampaignSystem.CampaignBehaviors.CraftingCampaignBehavior;

namespace GameInterface.Services.Smithing.Handlers;

internal class CraftingCampaignBehaviorTickHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehaviorTickHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ICraftingCampaignBehaviorInterface craftingCampaignBehaviorInterface;

    public CraftingCampaignBehaviorTickHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ICraftingCampaignBehaviorInterface craftingCampaignBehaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.craftingCampaignBehaviorInterface = craftingCampaignBehaviorInterface;

        messageBroker.Subscribe<HourTicked>(Handle_HourTicked);
        messageBroker.Subscribe<NetworkHourlyTick>(Handle_NetworkHourlyTick);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<HourTicked>(Handle_HourTicked);
        messageBroker.Unsubscribe<NetworkHourlyTick>(Handle_NetworkHourlyTick);
    }

    private void Handle_HourTicked(MessagePayload<HourTicked> obj)
    {
        if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out var craftingBehavior)) return;

        Dictionary<string, int> heroIdCraftingRecords = new Dictionary<string, int>();
        foreach(KeyValuePair<Hero, HeroCraftingRecord> keyValuePair in craftingBehavior._heroCraftingRecords)
        {
            if (!objectManager.TryGetIdWithLogging(keyValuePair.Key, out var currentHeroId)) return;

            heroIdCraftingRecords[currentHeroId] = keyValuePair.Value.CraftingStamina;
        }

        network.SendAll(new NetworkHourlyTick(heroIdCraftingRecords));
    }

    private void Handle_NetworkHourlyTick(MessagePayload<NetworkHourlyTick> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out var craftingBehavior)) return;

            if (data.HeroIdCraftingRecords == null) return;

            var heroCraftingRecords = craftingBehavior._heroCraftingRecords;
            foreach (KeyValuePair<string, int> keyValuePair in data.HeroIdCraftingRecords)
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(keyValuePair.Key, out var currentHero)) return;

                heroCraftingRecords[currentHero] = new HeroCraftingRecord(keyValuePair.Value);
            }

            // Needed because crafting stamina recovers as time passes while a client is in the crafting menu (unlike vanilla)
            messageBroker.Publish(this, new RefreshCraftingVM());
        });
    }
}
