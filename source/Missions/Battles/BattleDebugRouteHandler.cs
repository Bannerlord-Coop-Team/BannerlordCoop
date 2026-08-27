using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

internal class BattleDebugRouteHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleDebugRouteHandler>();

    private readonly IMessageBroker messageBroker;

    public BattleDebugRouteHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<NetworkRouteBattleEnemies>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRouteBattleEnemies>(Handle);
    }

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
