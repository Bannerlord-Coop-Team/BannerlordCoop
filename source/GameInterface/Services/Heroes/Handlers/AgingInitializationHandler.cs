using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Inventory.TradeSkills.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Handlers;

internal class AgingInitializationHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<AgingInitializationHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IAgingCampaignBehaviorInterface agingCampaignBehaviorInterface;

    private AgingPlayerData agingPlayerData;

    public AgingInitializationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IAgingCampaignBehaviorInterface agingCampaignBehaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.agingCampaignBehaviorInterface = agingCampaignBehaviorInterface;

        messageBroker.Subscribe<InitializeClientAgingData>(Handle);
        messageBroker.Subscribe<PlayerHeroChanged>(Handle);
        messageBroker.Subscribe<NetworkInitializeServerAgingDataKeys>(Handle);

        messageBroker.Subscribe<NetworkUpdatePlayerIllDays>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<InitializeClientAgingData>(Handle);
        messageBroker.Unsubscribe<PlayerHeroChanged>(Handle);
        messageBroker.Unsubscribe<NetworkInitializeServerAgingDataKeys>(Handle);

        messageBroker.Unsubscribe<NetworkUpdatePlayerIllDays>(Handle);
    }

    private void Handle(MessagePayload<InitializeClientAgingData> obj)
    {
        agingPlayerData = obj.What.AgingPlayerData;
    }

    // Need to load trade data when the hero changes for the player
    private void Handle(MessagePayload<PlayerHeroChanged> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.NewHero, out string playerHeroId)) return;

        Campaign.Current.MainHeroIllDays = GetMainHeroIllDays(playerHeroId);

        network.SendAll(new NetworkInitializeServerAgingDataKeys(playerHeroId));
    }

    private void Handle(MessagePayload<NetworkInitializeServerAgingDataKeys> obj)
    {
        GameThread.RunSafe(() =>
        {
            agingCampaignBehaviorInterface.AddPlayerKeys(obj.What.PlayerHeroId);
        });
    }

    private int GetMainHeroIllDays(string playerHeroId)
    {
        // Null and key check for players without existing aging data
        if (agingPlayerData?.PlayerIsIllDays?.ContainsKey(playerHeroId) != true) return -1;

        return agingPlayerData.PlayerIsIllDays[playerHeroId];
    }

    private void Handle(MessagePayload<NetworkUpdatePlayerIllDays> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.PlayerHeroId, out var playerHero)) return;

            if (playerHero != Hero.MainHero) return;

            Campaign.Current.MainHeroIllDays = data.NewIllDays;
        });
    }
}
