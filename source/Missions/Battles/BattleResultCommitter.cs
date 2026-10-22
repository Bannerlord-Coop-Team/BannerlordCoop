using Common.Logging;
using Common.Network;
using Missions.Messages;
using Serilog;
using System;
using TaleWorlds.Core;

namespace Missions.Battles;

/// <summary>
/// Reports a concluded coop battle's <see cref="MissionResult"/> to the campaign server. The server reconciles
/// every current mission member before it applies the shared map-event result.
/// </summary>
public interface IBattleResultCommitter
{
    /// <summary>Report a resolved native mission result without waiting for the player to leave.</summary>
    void ReportResolvedResult(MissionResult result);

    /// <summary>Remember the host's resolved state until this client's deployment is safe to conclude.</summary>
    void AcceptResolvedState(BattleState battleState);

    /// <summary>
    /// Report the remembered state at the current host epoch, if one exists. The server keeps each member's last report, so
    /// a state already reported at this epoch is not sent again; a new epoch (host migration) reports again.
    /// </summary>
    void ReportAcceptedResult();

    /// <summary>Get the resolved state for a late joiner's mesh catch-up.</summary>
    bool TryGetResolvedState(out BattleState battleState);

    /// <summary>
    /// [Game thread] Runs <paramref name="beforeReport"/> right before each result report goes out, so a battle type's own report
    /// (the naval ship fates) reaches the server ahead of it on the same relay channel; null clears it.
    /// </summary>
    void SetBeforeReport(Action beforeReport);
}

/// <inheritdoc cref="IBattleResultCommitter"/>
public class BattleResultCommitter : IBattleResultCommitter
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleResultCommitter>();

    private readonly object resolvedStateGate = new object();
    private readonly IBattleNetwork battleNetwork;
    private readonly INetwork relayNetwork;
    private readonly IBattleSession session;
    private BattleState resolvedState;
    private BattleState reportedState;
    private int reportedEpoch;
    private Action beforeReport;

    public BattleResultCommitter(IBattleNetwork battleNetwork, INetwork relayNetwork, IBattleSession session)
    {
        this.battleNetwork = battleNetwork;
        this.relayNetwork = relayNetwork;
        this.session = session;
    }

    public void ReportResolvedResult(MissionResult result)
    {
        if (result == null)
        {
            return;
        }

        if (!result.BattleResolved)
        {
            return;
        }

        if (!session.HasInstance)
        {
            return;
        }

        AcceptResolvedState(result.BattleState);
        ReportAcceptedResult();
    }

    public void AcceptResolvedState(BattleState battleState)
    {
        if (!IsSupportedResultState(battleState)) return;

        lock (resolvedStateGate)
        {
            resolvedState = battleState;
        }
    }

    public void ReportAcceptedResult()
    {
        if (!session.HasInstance || !TryGetResolvedState(out var battleState))
            return;

        int hostEpoch = session.HostEpoch;
        lock (resolvedStateGate)
        {
            if (IsAlreadyReported(reportedState, reportedEpoch, battleState, hostEpoch)) return;

            reportedState = battleState;
            reportedEpoch = hostEpoch;
        }

        Logger.Information("[BattleSync] Reporting resolved mission result {State} for instance {Instance} at host epoch {Epoch}",
            battleState, session.InstanceId, hostEpoch);
        beforeReport?.Invoke();
        relayNetwork.SendAll(new NetworkBattleResultReady(session.InstanceId, battleState, hostEpoch));

        if (session.IsLocalHost)
        {
            battleNetwork.SendAll(new NetworkBattleResultSnapshot(
                session.InstanceId,
                session.OwnControllerId,
                hostEpoch,
                battleState));
        }
    }

    public void SetBeforeReport(Action beforeReport) => this.beforeReport = beforeReport;

    internal static bool IsAlreadyReported(BattleState reportedState, int reportedEpoch, BattleState battleState, int hostEpoch)
        => IsSupportedResultState(reportedState) && reportedState == battleState && reportedEpoch == hostEpoch;

    public bool TryGetResolvedState(out BattleState battleState)
    {
        lock (resolvedStateGate)
        {
            battleState = resolvedState;
        }

        return IsSupportedResultState(battleState);
    }

    internal static bool IsSupportedResultState(BattleState battleState)
        => battleState == BattleState.AttackerVictory
            || battleState == BattleState.DefenderVictory
            || battleState == BattleState.DefenderPullBack;
}
