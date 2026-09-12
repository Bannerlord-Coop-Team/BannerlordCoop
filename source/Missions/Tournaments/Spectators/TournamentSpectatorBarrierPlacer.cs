using System;
using System.Collections.Generic;
using Common.Logging;
using Serilog;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Tournaments.Spectators;

public class TournamentSpectatorBarrierPlacer : MissionLogic
{
    private static readonly ILogger Logger = LogManager.GetLogger<TournamentSpectatorBarrierPlacer>();
    private IReadOnlyList<TournamentSpectatorBarrierData> barriers = Array.Empty<TournamentSpectatorBarrierData>();
    private Agent constrainedAgent;
    private Vec3 previousPosition;

    public override void EarlyStart()
    {
        base.EarlyStart();
        if (!TournamentSpectatorSceneLayouts.TryGet(Mission.SceneName, out var layout)) return;

        barriers = layout.Barriers;
        Logger.Information(
            "[TournamentSpectator] Loaded {BarrierCount} invisible spectator boundaries for {SceneName}",
            layout.Barriers.Count,
            Mission.SceneName);
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        Agent agent = Mission.MainAgent;
        CoopTournamentController controller = Mission.GetMissionBehavior<CoopTournamentController>();
        if (agent == null || !agent.IsActive() || controller == null || !controller.IsSpectatorAgent(agent))
        {
            constrainedAgent = null;
            return;
        }

        Vec3 currentPosition = agent.Position;
        if (!ReferenceEquals(agent, constrainedAgent))
        {
            constrainedAgent = agent;
            previousPosition = currentPosition;
            return;
        }

        foreach (TournamentSpectatorBarrierData barrier in barriers)
        {
            if (!TournamentSpectatorBarrierCollision.TryConstrain(
                    previousPosition,
                    currentPosition,
                    barrier,
                    out Vec3 constrainedPosition))
                continue;
            agent.TeleportToPosition(constrainedPosition);
            previousPosition = constrainedPosition;
            return;
        }

        previousPosition = currentPosition;
    }
}