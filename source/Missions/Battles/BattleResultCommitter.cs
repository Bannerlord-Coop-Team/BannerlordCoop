using Common.Logging;
using Common.Network;
using Missions.Messages;
using Serilog;
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

    /// <summary>Report the remembered state at the current host epoch, if one exists.</summary>
    void ReportAcceptedResult();

    /// <summary>Get the resolved state for a late joiner's mesh catch-up.</summary>
    bool TryGetResolvedState(out BattleState battleState);
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
        if (battleState != BattleState.AttackerVictory && battleState != BattleState.DefenderVictory)
            return;

        lock (resolvedStateGate)
        {
            resolvedState = battleState;
        }
    }

    public void ReportAcceptedResult()
    {
        if (!session.HasInstance || !TryGetResolvedState(out var battleState))
            return;

        Logger.Information("[BattleSync] Reporting resolved mission result {State} for instance {Instance}",
            battleState, session.InstanceId);
        relayNetwork.SendAll(new NetworkBattleResultReady(session.InstanceId, battleState, session.HostEpoch));

        if (session.IsLocalHost)
        {
            battleNetwork.SendAll(new NetworkBattleResultSnapshot(
                session.InstanceId,
                session.OwnControllerId,
                session.HostEpoch,
                battleState));
        }
    }

    public bool TryGetResolvedState(out BattleState battleState)
    {
        lock (resolvedStateGate)
        {
            battleState = resolvedState;
        }

        return battleState == BattleState.AttackerVictory || battleState == BattleState.DefenderVictory;
    }
}
