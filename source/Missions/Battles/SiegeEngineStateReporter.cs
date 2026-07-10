using Common.Logging;
using Common.Network;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using SandBox.Missions.MissionLogics;
using Serilog;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// [Host] Reports the surviving siege engine states to the server when the local siege mission ends.
/// Engine damage persists whether the assault resolved or the attackers retreated, so this runs on
/// every host mission end — before the result commit, so the server applies it while the siege event
/// still exists. Same host + successor gates as <see cref="BattleResultCommitter"/>.
/// </summary>
public interface ISiegeEngineStateReporter
{
    void ReportIfHost();
}

/// <inheritdoc cref="ISiegeEngineStateReporter"/>
public class SiegeEngineStateReporter : ISiegeEngineStateReporter
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEngineStateReporter>();

    private readonly IObjectManager objectManager;
    private readonly IBattleSession session;
    private readonly IBattleHostRegistry hostRegistry;
    private readonly INetwork relayNetwork;

    public SiegeEngineStateReporter(IObjectManager objectManager, IBattleSession session, IBattleHostRegistry hostRegistry, INetwork relayNetwork)
    {
        this.objectManager = objectManager;
        this.session = session;
        this.hostRegistry = hostRegistry;
        this.relayNetwork = relayNetwork;
    }

    public void ReportIfHost()
    {
        if (!session.IsLocalHost) return;

        // Leaving with successors still fighting is a retreat/handoff, not the battle's end — the last
        // player out reports, matching BattleResultCommitter's commit gate.
        if (hostRegistry.TryGet(session.InstanceId, out var assignment) && assignment.SuccessorControllerIds.Count > 0) return;

        if (!objectManager.TryGetObject<MapEvent>(session.InstanceId, out var mapEvent)) return;
        if (!mapEvent.IsSiegeAssault) return;

        var enginesLogic = Mission.Current?.GetMissionBehavior<MissionSiegeEnginesLogic>();
        if (enginesLogic == null) return;

        enginesLogic.GetMissionSiegeWeapons(out var defenderWeapons, out var attackerWeapons);

        Logger.Information("[BattleSync] Reporting final siege engine states for instance {Instance}", session.InstanceId);
        relayNetwork.SendAll(new NetworkSiegeEngineStatesReport(session.InstanceId,
            SiegeEngineStateConverter.ToEngineStates(attackerWeapons),
            SiegeEngineStateConverter.ToEngineStates(defenderWeapons)));
    }
}
