using Common.Logging;
using Common.Network;
using Missions.Messages;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Reports a concluded coop battle's <see cref="MissionResult"/> to the campaign server. The server reconciles
/// every current mission member before it applies the shared map-event result.
/// </summary>
public interface IBattleResultCommitter
{
    /// <summary>
    /// Report the current mission result during mission teardown as a fallback for result-ready callbacks.
    /// </summary>
    void CommitResolvedResult();

    /// <summary>Report a resolved native mission result without waiting for the player to leave.</summary>
    void ReportResolvedResult(MissionResult result);
}

/// <inheritdoc cref="IBattleResultCommitter"/>
public class BattleResultCommitter : IBattleResultCommitter
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleResultCommitter>();

    private readonly INetwork network;
    private readonly IBattleSession session;

    public BattleResultCommitter(INetwork network, IBattleSession session)
    {
        this.network = network;
        this.session = session;
    }

    public void CommitResolvedResult()
    {
        ReportResolvedResult(Mission.Current?.MissionResult);
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

        Logger.Information("[BattleSync] Reporting resolved mission result {State} for instance {Instance}",
            result.BattleState, session.InstanceId);
        network.SendAll(new NetworkBattleResultReady(session.InstanceId, result.BattleState, session.HostEpoch));
    }
}
