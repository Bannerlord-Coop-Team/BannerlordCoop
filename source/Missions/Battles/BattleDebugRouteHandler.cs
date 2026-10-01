using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
#if DEBUG
using GameInterface;
using GameInterface.Services.GameDebug.Commands;
#endif

namespace Missions.Battles;

internal class BattleDebugRouteHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleDebugRouteHandler>();

    private readonly IMessageBroker messageBroker;

    public BattleDebugRouteHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<NetworkRouteBattleEnemies>(Handle);
#if DEBUG
        messageBroker.Subscribe<NetworkBattleHealthFixture>(HandleHealthFixture);
#endif
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRouteBattleEnemies>(Handle);
#if DEBUG
        messageBroker.Unsubscribe<NetworkBattleHealthFixture>(HandleHealthFixture);
#endif
    }

#if DEBUG
    private void HandleHealthFixture(MessagePayload<NetworkBattleHealthFixture> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() =>
        {
            var request = payload.What;
            var mission = Mission.Current;
            var controller = mission?.GetMissionBehavior<CoopBattleController>();
            if (controller == null || controller.Session.InstanceId != request.MapEventId ||
                mission.MissionIsEnding ||
                mission.MissionResult?.BattleResolved == true)
                return;
            if (request.Operation == "deploy")
            {
                if (mission.GetMissionBehavior<DeploymentMissionController>()?.TeamSetupOver == true)
                    mission.GetMissionBehavior<DeploymentHandler>()?.FinishDeployment();
                return;
            }
            if (!controller.Deployment.IsCommitted) return;
            if (request.Operation == "retreat")
            {
                mission.RetreatMission();
                return;
            }
            if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry) ||
                !registry.TryGetAgentInfo(request.AgentId, out var info) ||
                info.CurrentAuthority != controller.Session.OwnControllerId ||
                info.Agent == null || !info.Agent.IsActive() || !info.Agent.IsHuman)
                return;
            var agent = info.Agent;
            float before = agent.Health;
            if (request.Operation == "damage" && request.Damage > 0 && request.Damage <= 10000)
            {
                var blow = new Blow(-1) { InflictedDamage = request.Damage };
                blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex = -1;
                agent.RegisterBlow(blow, default);
            }
            else if (request.Operation == "rout")
                agent.Retreat(mission.GetClosestFleePositionForAgent(agent));
            else
                return;
            Logger.Information("[HealthFixture] {Operation} agent={AgentId} before={Before} after={After} active={Active}",
                request.Operation, request.AgentId, before, agent.Health, agent.IsActive());
        }, context: nameof(HandleHealthFixture));
    }
#endif

    private void Handle(MessagePayload<NetworkRouteBattleEnemies> payload)
    {
        if (ModInformation.IsServer) return;

        GameThread.RunSafe(() =>
        {
            var mission = Mission.Current;
            var controller = mission?.GetMissionBehavior<CoopBattleController>();
            var playerTeam = mission?.PlayerTeam;
            if (mission == null ||
                controller == null ||
                playerTeam == null ||
                controller.Session.InstanceId != payload.What.MapEventId ||
                !controller.Session.IsLocalHost)
            {
                return;
            }

            var enemySide = playerTeam.Side == BattleSideEnum.Attacker
                ? BattleSideEnum.Defender
                : BattleSideEnum.Attacker;
            var enemies = mission.Agents
                .Where(agent =>
                    agent.IsActive() &&
                    agent.IsHuman &&
                    agent.Team?.Side == enemySide &&
                    !agent.IsRunningAway)
                .ToArray();
            var routeCount = Math.Max(0, enemies.Length - payload.What.EnemiesToLeaveFighting);

            for (int i = 0; i < routeCount; i++)
                enemies[i].Retreat(mission.GetClosestFleePositionForAgent(enemies[i]));

            Logger.Information(
                "[BattleDebug] Ordered {RoutedCount}/{EnemyCount} authoritative enemies to retreat for {MapEventId}",
                routeCount,
                enemies.Length,
                payload.What.MapEventId);
        });
    }
}
