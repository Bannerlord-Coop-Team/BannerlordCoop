using Common;
using Common.Messaging;
using Common.Network;
using Coop.Naval.CoopSession.Interfaces;
using Coop.Naval.CoopSession.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.NavalDLC;
using GameInterface.Services.NavalDLC.Messages;
using GameInterface.Services.ObjectManager;
using NavalDLC.CampaignBehaviors;
using TaleWorlds.CampaignSystem;

namespace Coop.Naval.CoopSession.Handlers;

internal class NavalInitializationHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionNavalPlayerDataInterface sessionNavalPlayerDataInterface;

    private NavalPlayerData navalPlayerData;

    public NavalInitializationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionNavalPlayerDataInterface sessionNavalPlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionNavalPlayerDataInterface = sessionNavalPlayerDataInterface;

        messageBroker.Subscribe<InitializeClientNavalData>(Handle);
        messageBroker.Subscribe<PlayerHeroChanged>(Handle);
        messageBroker.Subscribe<NetworkInitializeServerNavalDataKeys>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<InitializeClientNavalData>(Handle);
        messageBroker.Unsubscribe<PlayerHeroChanged>(Handle);
        messageBroker.Unsubscribe<NetworkInitializeServerNavalDataKeys>(Handle);
    }

    private void Handle(MessagePayload<InitializeClientNavalData> obj)
    {
        navalPlayerData = obj.What.NavalPlayerData;
    }

    // Need to load naval data when the hero changes for the player
    private void Handle(MessagePayload<PlayerHeroChanged> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.NewHero, out string playerHeroId)) return;

        NavalDLCFigureheadCampaignBehavior figureheadCampaignBehavior = Campaign.Current.GetCampaignBehavior<NavalDLCFigureheadCampaignBehavior>();

        Campaign.Current.UnlockedFigureheadsByMainHero = sessionNavalPlayerDataInterface.GetUnlockedFigureheads(navalPlayerData, playerHeroId);
        figureheadCampaignBehavior._lastFigureheadLootTime = GetLastFigureheadLootTime(playerHeroId);

        network.SendAll(new NetworkInitializeServerNavalDataKeys(playerHeroId));
    }

    private void Handle(MessagePayload<NetworkInitializeServerNavalDataKeys> obj)
    {
        GameThread.RunSafe(() =>
        {
            sessionNavalPlayerDataInterface.AddPlayerKeys(obj.What.PlayerHeroId);
        });
    }

    private CampaignTime GetLastFigureheadLootTime(string playerHeroId)
    {
        // Null and key check for players with the default figurehead loot time
        var lootTimes = navalPlayerData?.PlayerLastFigureheadLootTimes;
        if (lootTimes == null || !lootTimes.TryGetValue(playerHeroId, out var lootTimeTicks)) return CampaignTime.Zero;

        return new CampaignTime(lootTimeTicks);
    }
}
