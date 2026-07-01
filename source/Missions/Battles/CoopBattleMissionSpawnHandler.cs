using Common.Logging;
using GameInterface.Services.MapEvents.TroopSupply;
using SandBox.Missions.MissionLogics;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Coop replacement for <see cref="SandBoxBattleMissionSpawnHandler"/>. The native handler sizes each side
/// from <c>MapEvent.GetNumberOfInvolvedMen(side)</c> — the FULL side — and feeds that to the spawn logic,
/// which then refuses to spawn a side until that many troops are reserved from the side's single supplier.
/// In coop each client supplies only the troops it OWNS (its own party; plus the AI/enemy side for the host),
/// so the full count is never reached and the side never spawns (the 22-vs-7 deadlock). This handler instead
/// sizes each side to exactly what THIS client's supplier provides, so the client fields its own troops at
/// once; every other party's troops arrive as puppets broadcast by their owner.
/// <para>
/// The reserve arrives on a separate network round trip from the mission-open message, so a side can be sized
/// before its reserve lands — <see cref="AfterStart"/> then sizes it to zero and it would never spawn.
/// <see cref="OnMissionTick"/> polls each such side and grows its spawn phase to the owned total once the
/// reserve arrives, so the side ends up correct no matter how the two messages interleave. Initial == total
/// (no staged reinforcement waves); the deployment controller decides WHEN the sized troops spawn (frozen
/// during deployment, released on Start Battle).
/// </para>
/// </summary>
public class CoopBattleMissionSpawnHandler : SandBoxMissionSpawnHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopBattleMissionSpawnHandler>();

    private readonly CoopTroopSupplier _defenderSupplier;
    private readonly CoopTroopSupplier _attackerSupplier;

    // A side whose reserve had not arrived when AfterStart sized it (sized to zero) stays pending until its
    // reserve lands and OnMissionTick grows the spawn phase. A side sized to a real count is never pending, so
    // a healthy side (and any battle-size distribution the native Init applied to it) is left untouched.
    private bool _defenderReservePending;
    private bool _attackerReservePending;

    public CoopBattleMissionSpawnHandler(CoopTroopSupplier defenderSupplier, CoopTroopSupplier attackerSupplier)
    {
        _defenderSupplier = defenderSupplier;
        _attackerSupplier = attackerSupplier;
    }

    public override void AfterStart()
    {
        int defenderOwned = _defenderSupplier.TotalTroops;
        int attackerOwned = _attackerSupplier.TotalTroops;

        if (!_defenderSupplier.IsPopulated || !_attackerSupplier.IsPopulated)
            Logger.Warning("[BattleSync] Coop spawn handler started before reserves arrived (Def populated={Def}, Atk populated={Atk}) — resizing on tick once they land",
                _defenderSupplier.IsPopulated, _attackerSupplier.IsPopulated);

        _missionAgentSpawnLogic.SetSpawnHorses(BattleSideEnum.Defender, !_mapEvent.IsSiegeAssault);
        _missionAgentSpawnLogic.SetSpawnHorses(BattleSideEnum.Attacker, !_mapEvent.IsSiegeAssault);

        // initial == total: size the whole owned set as one (no staged reinforcement waves). The deployment
        // controller gates when these actually spawn — frozen in formation during deployment, released on
        // Start Battle (FinishDeployment).
        var settings = CreateSandBoxBattleWaveSpawnSettings();
        _missionAgentSpawnLogic.InitWithSinglePhase(defenderOwned, attackerOwned, defenderOwned, attackerOwned, spawnDefenders: true, spawnAttackers: true, in settings);

        // A side sized to zero here had no reserve yet; grow it on tick once the reserve arrives. A side sized
        // to a real count is final (the reserve replaces the whole owned set in one SetReserve), so it is not pending.
        _defenderReservePending = defenderOwned == 0;
        _attackerReservePending = attackerOwned == 0;

        Logger.Information("[BattleSync] Coop spawn sized to owned counts: Defender={Def}, Attacker={Atk}", defenderOwned, attackerOwned);
    }

    // Grow a side whose reserve landed after AfterStart sized it to zero. Runs on the game thread (after
    // AfterStart, so the phases exist), reading the supplier's lock-guarded getters that the network-thread
    // SetReserve writes — no callback or marshaling. Each side latches once settled, so a mid-battle migration
    // re-feed (which grows a supplier that is already populated) is left to the adopt/puppet path.
    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);

        if (_defenderReservePending)
            _defenderReservePending = TryResolvePendingSide(BattleSideEnum.Defender, _defenderSupplier, _missionAgentSpawnLogic.DefenderActivePhase);
        if (_attackerReservePending)
            _attackerReservePending = TryResolvePendingSide(BattleSideEnum.Attacker, _attackerSupplier, _missionAgentSpawnLogic.AttackerActivePhase);
    }

    // Returns whether the side is still pending. Once the supplier is populated the side settles: if it owns
    // troops the phase is grown to the owned total (CheckDeployment then reserves and spawns the difference),
    // and an empty non-owned side just settles with no growth (its troops arrive as puppets).
    private bool TryResolvePendingSide(BattleSideEnum side, CoopTroopSupplier supplier, MissionSpawnPhase phase)
    {
        var (stillPending, resize, newTotal) = ResolvePendingSide(supplier.IsPopulated, supplier.TotalTroops);
        if (resize)
        {
            if (phase == null) return true; // phase not built yet (not expected after AfterStart) — retry next tick
            GrowSide(side, phase, newTotal);
        }
        return stillPending;
    }

    private void GrowSide(BattleSideEnum side, MissionSpawnPhase phase, int ownedTotal)
    {
        // Native Init sizes the phase AND the Mission's agent counts together; it sized this side to zero, so
        // mirror both here (_numberOfTroopsInTotal is the reported side strength; the two agent counts feed the
        // casualty-ratio morale model, which otherwise stays at a zero ratio). The battle count is the min of
        // both sides' totals, so a non-owned empty side keeps it at zero, as native Init does on the on-time path.
        _missionAgentSpawnLogic._numberOfTroopsInTotal[(int)side] += ownedTotal - phase.TotalSpawnNumber;
        phase.TotalSpawnNumber = ownedTotal;
        phase.InitialSpawnNumber = ownedTotal;
        phase.RemainingSpawnNumber = 0;

        Mission.SetInitialAgentCountForSide(side, ownedTotal);
        // Assign the battle count directly rather than via SetBattleAgentCount: when both sides start empty, Init's
        // 0/0 battle-size split can leave it at a negative sentinel, and that setter only lowers or replaces zero,
        // so it could not raise it back.
        Mission._agentCount = Math.Min(
            _missionAgentSpawnLogic.DefenderActivePhase.TotalSpawnNumber,
            _missionAgentSpawnLogic.AttackerActivePhase.TotalSpawnNumber);

        Logger.Information("[BattleSync] {Side} reserve arrived after sizing; grew spawn to {Count}", side, ownedTotal);
    }

    /// <summary>
    /// Pure per-tick decision for a pending side, split out so it is unit-testable without a live mission. While
    /// the reserve has not arrived (<paramref name="populated"/> false) the side stays pending. Once it has, the
    /// side settles; a resize is requested only when the owned total is positive (an empty non-owned side reports
    /// zero and is left for puppet spawns).
    /// </summary>
    public static (bool stillPending, bool resize, int newTotal) ResolvePendingSide(bool populated, int ownedTotal)
    {
        return (stillPending: !populated, resize: populated && ownedTotal > 0, newTotal: ownedTotal);
    }
}
