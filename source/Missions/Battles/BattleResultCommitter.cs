using Common.Logging;
using GameInterface.Services.MapEvents;
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
    /// Commit this concluded battle's result. Setting <c>MapEvent.BattleState</c> runs the native
    /// setter, which the coop intercept (<c>MapEventPatches</c>) syncs to the server; there it runs
    /// <c>OnBattleWon</c> (capturing the defeated players) and the auto-finalize (closing every player's
    /// encounter). A retreat (unresolved result) commits nothing; an already-resolved state is left untouched.
    /// This is intentionally not host-gated: the first client to finish the battle and click Done must
    /// resolve the shared campaign battle for everyone.
    /// </summary>
    void CommitResolvedResult();
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

    public void CommitResolvedResult()
    {
        // The Done button can produce the first resolved result on any client.
        // Retreat stays ignored by the BattleResolved guard below because it does not end the shared encounter.
        var result = Mission.Current?.MissionResult;
        if (result == null)
        {
            return;
        }

        if (!result.BattleResolved)
        {
            return;
        }

        if (!objectManager.TryGetObject<MapEvent>(session.InstanceId, out var mapEvent))
        {
            return;
        }

        if (mapEvent.BattleState != BattleState.None)
        {
            return;
        }

        // If the current host is leaving while successors are still assigned, this mission end is a retreat or
        // host handoff, not the shared battle's Done result. A successor will commit once the battle truly ends.
        if (session.IsLocalHost &&
            hostRegistry.TryGet(session.InstanceId, out var assignment) &&
            assignment.SuccessorControllerIds.Count > 0)
        {
            Logger.Information("[BattleSync] Not committing battle result for {Instance}: {Count} successor(s) still in the battle",
                session.InstanceId,
                assignment.SuccessorControllerIds.Count);
            return;
        }

        Logger.Information("[BattleSync] Committing concluded battle result {State} for instance {Instance}; localHost={LocalHost}",
            result.BattleState,
            session.InstanceId,
            session.IsLocalHost);
        mapEvent.BattleState = result.BattleState;
    }
}
