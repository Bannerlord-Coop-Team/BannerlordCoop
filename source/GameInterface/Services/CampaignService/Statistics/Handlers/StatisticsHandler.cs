using Common;
using Common.Messaging;
using Common.Network.Messages;
using GameInterface.Services.CampaignService.Statistics.Interfaces;
using GameInterface.Services.CampaignService.Statistics.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.CampaignService.Statistics.Handlers;

internal class StatisticsHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IStatisticsCampaignBehaviorInterface statisticsInterface;

    public StatisticsHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IStatisticsCampaignBehaviorInterface statisticsInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.statisticsInterface = statisticsInterface;

        messageBroker.Subscribe<PlayerConnectionStateChanged>(Handle_PlayerConnectionStateChanged);
        messageBroker.Subscribe<NetworkMissionStatistics>(Handle_NetworkMissionStatistics);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerConnectionStateChanged>(Handle_PlayerConnectionStateChanged);
        messageBroker.Unsubscribe<NetworkMissionStatistics>(Handle_NetworkMissionStatistics);
    }

    private void Handle_PlayerConnectionStateChanged(MessagePayload<PlayerConnectionStateChanged> obj)
    {
        if (ModInformation.IsClient) return;

        GameThread.RunSafe(() =>
        {
            statisticsInterface.UpdatePlayTime();
        });
    }

    private void Handle_NetworkMissionStatistics(MessagePayload<NetworkMissionStatistics> obj)
    {
        if (ModInformation.IsClient) return;

        var data = obj.What;
        
        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, data.PlayerHeroId)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.PlayerHeroId, out var playerHero)) return;

            statisticsInterface.OnMissionStatisticsReceived(playerHero, data);
        });
    }
}
