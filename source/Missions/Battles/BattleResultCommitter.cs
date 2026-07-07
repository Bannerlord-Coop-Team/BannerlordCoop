using Common.Logging;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Commits a concluded coop battle's <see cref="MissionResult"/> to the campaign map event on mission end.
/// A live coop battle never sets the map event's <c>BattleState</c> on its own (the encounter doesn't
/// resolve), so without this the defeated players are left uncaptured with the encounter still open.
/// </summary>
public interface IBattleResultCommitter
{
    /// <summary>
    /// [Host] Commit this concluded battle's result. Setting <c>MapEvent.BattleState</c> runs the native
    /// setter, which the coop intercept (<c>MapEventPatches</c>) syncs to the server — there it runs
    /// <c>OnBattleWon</c> (capturing the defeated players) and the auto-finalize (closing every player's
    /// encounter). Only the host commits; a retreat (unresolved result) commits nothing; an already-resolved
    /// state (e.g. a simulated battle) is left untouched.
    /// </summary>
    void CommitIfHost();
}

/// <inheritdoc cref="IBattleResultCommitter"/>
public class BattleResultCommitter : IBattleResultCommitter
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleResultCommitter>();

    private readonly IObjectManager objectManager;
    private readonly IBattleSession session;
    private readonly IBattleHostRegistry hostRegistry;

    public BattleResultCommitter(IObjectManager objectManager, IBattleSession session, IBattleHostRegistry hostRegistry)
    {
        this.objectManager = objectManager;
        this.session = session;
        this.hostRegistry = hostRegistry;
    }

    public void CommitIfHost()
    {
        if (!session.IsLocalHost) return;

        // Other players are still fighting this battle: leaving now is a RETREAT/handoff, not the battle's
        // conclusion — a successor is promoted and the battle continues under it. Committing here would end
        // the battle for everyone: a host retreating after losing its own troops carries a RESOLVED defeat
        // MissionResult, and committing that made the server run the full conclusion (OnBattleWon captured the
        // still-fighting players, the auto-finalize closed their encounters and destroyed the map event under
        // their live mission). The LAST player out — then the host, with an empty successor line — commits.
        if (hostRegistry.TryGet(session.InstanceId, out var assignment) && assignment.SuccessorControllerIds.Count > 0)
        {
            Logger.Information("[BattleSync] Not committing battle result for {Instance}: {Count} successor(s) still in the battle",
                session.InstanceId, assignment.SuccessorControllerIds.Count);
            return;
        }

        var result = Mission.Current?.MissionResult;
        if (result == null || !result.BattleResolved) return;

        if (!objectManager.TryGetObject<MapEvent>(session.InstanceId, out var mapEvent)) return;
        if (mapEvent.BattleState != BattleState.None) return;

        Logger.Information("[BattleSync] Committing concluded battle result {State} for instance {Instance}", result.BattleState, session.InstanceId);
        mapEvent.BattleState = result.BattleState;
    }
}
