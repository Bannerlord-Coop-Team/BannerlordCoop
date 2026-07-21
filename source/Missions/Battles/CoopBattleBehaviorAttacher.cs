using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
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

    public CoopBattleBehaviorAttacher(
        Func<CoopBattleController> controllerFactory,
        IMessageBroker messageBroker)
    {
        this.controllerFactory = controllerFactory;
        this.messageBroker = messageBroker;
    }

    public void Attach(Mission mission)
    {
        var controller = controllerFactory();
        mission.AddMissionBehavior(controller);
        mission.AddMissionBehavior(new BattleResultReadyLogic(
            controller.ResultCommitter,
            controller.SiegeEngineStateReporter,
            messageBroker,
            controller.Session));
        Logger.Information("[BattleSync] Attached coop battle behaviors to mission '{Scene}'", mission.SceneName);
    }
}
