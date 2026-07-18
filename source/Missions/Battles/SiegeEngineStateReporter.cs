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
/// Engine damage persists before a concluded result, with mission teardown as the retreat fallback.
/// </summary>
public interface ISiegeEngineStateReporter
{
    void ReportConcludedIfHost();
    void ReportOnLeavingIfHost();
}

/// <inheritdoc cref="ISiegeEngineStateReporter"/>
public class SiegeEngineStateReporter : ISiegeEngineStateReporter
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEngineStateReporter>();

    private readonly IObjectManager objectManager;
    private readonly IBattleSession session;
    private readonly IBattleHostRegistry hostRegistry;
    private readonly INetwork relayNetwork;
    private bool hasReported;

    public SiegeEngineStateReporter(IObjectManager objectManager, IBattleSession session, IBattleHostRegistry hostRegistry, INetwork relayNetwork)
    {
        this.objectManager = objectManager;
        this.session = session;
        this.hostRegistry = hostRegistry;
        this.relayNetwork = relayNetwork;
    }

    public void ReportConcludedIfHost()
    {
        ReportIfHost(requireNoSuccessors: false);
    }

    public void ReportOnLeavingIfHost()
    {
        ReportIfHost(requireNoSuccessors: true);
    }

    private void ReportIfHost(bool requireNoSuccessors)
    {
        if (hasReported || !session.IsLocalHost) return;

        // Leaving with successors still fighting is a retreat/handoff, not the battle's end.
        bool hasAssignment = hostRegistry.TryGet(session.InstanceId, out var assignment);
        if (requireNoSuccessors && hasAssignment && assignment.SuccessorControllerIds.Count > 0) return;

        if (!objectManager.TryGetObject<MapEvent>(session.InstanceId, out var mapEvent)) return;
        if (!mapEvent.IsSiegeAssault) return;

        var enginesLogic = Mission.Current?.GetMissionBehavior<MissionSiegeEnginesLogic>();
        if (enginesLogic == null) return;

        enginesLogic.GetMissionSiegeWeapons(out var defenderWeapons, out var attackerWeapons);

        Logger.Information("[BattleSync] Reporting final siege engine states for instance {Instance}", session.InstanceId);
        // BR-102: stamp the report with our hosting generation so the server can refuse a stale one.
        relayNetwork.SendAll(new NetworkSiegeEngineStatesReport(session.InstanceId,
            SiegeEngineStateConverter.ToEngineStates(attackerWeapons),
            SiegeEngineStateConverter.ToEngineStates(defenderWeapons),
            hasAssignment ? assignment.Epoch : 0));
        hasReported = true;
    }
}
