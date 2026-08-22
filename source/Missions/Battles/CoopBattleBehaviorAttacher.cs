using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Time.UI;
using GameInterface.Services.UI.PlayerNameplates;
using Serilog;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <inheritdoc cref="ICoopBattleBehaviorAttacher"/>
internal class CoopBattleBehaviorAttacher : ICoopBattleBehaviorAttacher
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopBattleBehaviorAttacher>();

    // Autofac-provided factory: CoopBattleController is registered InstancePerDependency, so each call
    // builds a fresh controller that lives and is disposed with its mission.
    private readonly Func<CoopBattleController> controllerFactory;
    private readonly IMessageBroker messageBroker;
    private readonly Func<MissionMapTimeView> mapTimeViewFactory;
    private readonly Func<PlayerNameplateMissionView> playerNameplateViewFactory;

    public CoopBattleBehaviorAttacher(
        Func<CoopBattleController> controllerFactory,
        Func<MissionMapTimeView> mapTimeViewFactory,
        Func<PlayerNameplateMissionView> playerNameplateViewFactory,
        IMessageBroker messageBroker)
    {
        this.controllerFactory = controllerFactory;
        this.mapTimeViewFactory = mapTimeViewFactory;
        this.playerNameplateViewFactory = playerNameplateViewFactory;
        this.messageBroker = messageBroker;
    }

    public void Attach(Mission mission)
    {
        var controller = controllerFactory();
        mission.AddMissionBehavior(controller);
        mission.AddMissionBehavior(mapTimeViewFactory());
        mission.AddMissionBehavior(playerNameplateViewFactory());
        mission.AddMissionBehavior(new BattleResultReadyLogic(
            controller.ResultCommitter,
            controller.SiegeEngineStateReporter,
            messageBroker,
            controller.Session,
            controller.Deployment));
        Logger.Information("[BattleSync] Attached coop battle behaviors to mission '{Scene}'", mission.SceneName);
    }
}
