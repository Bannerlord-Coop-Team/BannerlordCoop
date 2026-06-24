using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Caravans.Interfaces;
using GameInterface.Services.Caravans.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Caravans.Handlers;

internal class CaravansCampaignBehaviorInitializationHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CaravansCampaignBehaviorInitializationHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionCaravansPlayerDataInterface sessionCaravansPlayerDataInterface;

    private CaravansPlayerData caravansPlayerData;

    public CaravansCampaignBehaviorInitializationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionCaravansPlayerDataInterface sessionCaravansPlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionCaravansPlayerDataInterface = sessionCaravansPlayerDataInterface;
        messageBroker.Subscribe<InitializeClientCaravansData>(Handle);
        messageBroker.Subscribe<PlayerHeroChanged>(Handle);
        messageBroker.Subscribe<NetworkInitializeServerCaravansDataKeys>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<InitializeClientCaravansData>(Handle);
        messageBroker.Unsubscribe<PlayerHeroChanged>(Handle);
        messageBroker.Unsubscribe<NetworkInitializeServerCaravansDataKeys>(Handle);
    }

    private void Handle(MessagePayload<InitializeClientCaravansData> obj)
    {
        caravansPlayerData = obj.What.CaravansPlayerData;
    }

    // Need to load caravan data when the hero changes for the player
    private void Handle(MessagePayload<PlayerHeroChanged> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.NewHero, out string playerHeroId)) return;

        CaravansCampaignBehavior caravansCampaignBehavior = Campaign.Current.GetCampaignBehavior<CaravansCampaignBehavior>();

        caravansCampaignBehavior._prohibitedKingdomsForPlayerCaravans = GetProhibitedKingdoms(playerHeroId);
        caravansCampaignBehavior._interactedCaravans = GetInteractedCaravans(playerHeroId);
        caravansCampaignBehavior._tradeRumorTakenCaravans = GetTradeRumorTakenCaravans(playerHeroId);

        network.SendAll(new NetworkInitializeServerCaravansDataKeys(playerHeroId));
    }

    private void Handle(MessagePayload<NetworkInitializeServerCaravansDataKeys> obj)
    {
        sessionCaravansPlayerDataInterface.AddPlayerKeys(obj.What.PlayerHeroId);
    }

    private List<Kingdom> GetProhibitedKingdoms(string playerHeroId)
    {
        var prohibitedKingdoms = new List<Kingdom>();

        // Null and key check for players without existing caravans data
        if (caravansPlayerData?.PlayerProhibitedKingdomsForPlayerCaravans?.ContainsKey(playerHeroId) != true) return prohibitedKingdoms;

        foreach (var kingdomId in caravansPlayerData.PlayerProhibitedKingdomsForPlayerCaravans[playerHeroId])
        {
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(kingdomId, out var kingdom)) continue;

            prohibitedKingdoms.Add(kingdom);
        }

        return prohibitedKingdoms;
    }

    private Dictionary<MobileParty, CaravansCampaignBehavior.PlayerInteraction> GetInteractedCaravans(string playerHeroId)
    {
        var interactedCaravans = new Dictionary<MobileParty, CaravansCampaignBehavior.PlayerInteraction>();

        // Null and key check for players without existing caravans data
        if (caravansPlayerData?.PlayerInteractedCaravans?.ContainsKey(playerHeroId) != true) return interactedCaravans;

        foreach (KeyValuePair<string, int> caravanInteraction in caravansPlayerData.PlayerInteractedCaravans[playerHeroId])
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(caravanInteraction.Key, out var caravan)) continue;

            interactedCaravans[caravan] = (CaravansCampaignBehavior.PlayerInteraction)caravanInteraction.Value;
        }

        return interactedCaravans;
    }

    private Dictionary<MobileParty, CampaignTime> GetTradeRumorTakenCaravans(string playerHeroId)
    {
        var tradeRumorTakenCaravans = new Dictionary<MobileParty, CampaignTime>();

        // Null and key check for players without existing caravans data
        if (caravansPlayerData?.PlayerTradeRumorTakenCaravans?.ContainsKey(playerHeroId) != true) return tradeRumorTakenCaravans;

        foreach (KeyValuePair<string, CampaignTime> tradeRumorTakenCaravan in caravansPlayerData.PlayerTradeRumorTakenCaravans[playerHeroId])
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(tradeRumorTakenCaravan.Key, out var caravan)) continue;

            tradeRumorTakenCaravans[caravan] = tradeRumorTakenCaravan.Value;
        }

        return tradeRumorTakenCaravans;
    }
}
