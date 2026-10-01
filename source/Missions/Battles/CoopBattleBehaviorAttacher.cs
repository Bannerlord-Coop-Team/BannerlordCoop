using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Time.UI;
using GameInterface.Services.UI.PlayerNameplates;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <inheritdoc cref="ICoopBattleBehaviorAttacher"/>
internal class CoopBattleBehaviorAttacher : ICoopBattleBehaviorAttacher
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopBattleBehaviorAttacher>();
    // Autofac-provided factory: CoopBattleController is registered InstancePerDependency, so each call
    // builds a fresh controller with its own mission lifetime.
    private readonly Func<CoopBattleController> controllerFactory;
    private readonly IMessageBroker messageBroker;
    private readonly INetwork relayNetwork;

    public CoopBattleBehaviorAttacher(
        Func<CoopBattleController> controllerFactory,
        IMessageBroker messageBroker,
        INetwork relayNetwork)
    {
        this.controllerFactory = controllerFactory;
        this.messageBroker = messageBroker;
        this.relayNetwork = relayNetwork;
    }

    public void Attach(Mission mission)
    {
        var controller = controllerFactory();
        var behaviors = new List<MissionBehavior> { controller };
        try
        {
#if DEBUG
            behaviors.Add(controller.ResolveMissionBehavior<SiegeInteractionDebugBehavior>());
#endif
            behaviors.Add(controller.ResolveMissionBehavior<MissionMapTimeView>());
            behaviors.Add(controller.ResolveMissionBehavior<PlayerNameplateMissionView>());
            behaviors.Add(new BattleResultReadyLogic(
                controller.ResultCommitter,
                controller.SiegeEngineStateReporter,
                messageBroker,
                controller.Session,
                controller.Deployment,
                relayNetwork));
            foreach (var behavior in behaviors) mission.AddMissionBehavior(behavior);
            Logger.Information("[BattleSync] Attached coop battle behaviors to mission '{Scene}'", mission.SceneName);
        }
        catch
        {
            try { controller.Abandon(); }
            catch (Exception error) { Logger.Error(error, "Failed battle attachment cleanup"); }
            foreach (var behavior in behaviors)
            {
                if (!mission.MissionBehaviors.Contains(behavior)) continue;
                try { mission.RemoveMissionBehavior(behavior); }
                catch (Exception error) { Logger.Error(error, "Failed to remove a partially attached battle behavior"); }
            }
            throw;
        }
    }
}
