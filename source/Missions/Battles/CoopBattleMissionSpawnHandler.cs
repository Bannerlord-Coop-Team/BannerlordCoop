using Common.Logging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using SandBox.Missions.MissionLogics;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Coop replacement for <see cref="SandBoxBattleMissionSpawnHandler"/>: sizes each side to what THIS client's
/// supplier owns (its party, plus the AI/enemy side for the host), not the full side the native handler waits on
/// and never fills. The server's initial-spawn entitlements are joint across both sides, so both are sized in one pass
/// once both reserves land: at <see cref="AfterStart"/> if already present, else held at zero until
/// <see cref="OnMissionTick"/> sees them, so a late side ends up identical to an on-time one.
/// </summary>
public class CoopBattleMissionSpawnHandler : SandBoxMissionSpawnHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopBattleMissionSpawnHandler>();

    // Hold this long for a still-in-flight reserve before sizing with whatever landed. A dropped or server-rejected
    // reserve request would otherwise never populate a supplier, and the deployment controller (which gates on
    // IsSized) would wedge on the loading screen forever. A partial response degrades to a one-sided battle; a
    // zero-troop response cannot produce a valid battle and is terminated through the normal mission lifecycle.
    private const float ReserveHoldDeadlineSeconds = 15f;

    private readonly CoopTroopSupplier _defenderSupplier;
    private readonly CoopTroopSupplier _attackerSupplier;

    // Latched once the sides are sized jointly; both are held at zero until then.
    private bool _sized;
    private int _defenderSizedReserveRevision = -1;
    private int _attackerSizedReserveRevision = -1;

    // Time spent holding both sides while a reserve is in flight (only accrues on the held path).
    private float _heldSeconds;
    private bool _emptyBattleAbortRequested;

    // Gated on by CoopBattleDeploymentMissionController: a game-thread latch, not the suppliers' network-thread
    // IsPopulated (which could read true mid-frame before Init has actually sized).
    public bool IsSized => _sized;

    public CoopBattleMissionSpawnHandler(CoopTroopSupplier defenderSupplier, CoopTroopSupplier attackerSupplier)
    {
        _defenderSupplier = defenderSupplier;
        _attackerSupplier = attackerSupplier;
    }

    public override void AfterStart()
    {
        _missionAgentSpawnLogic.SetSpawnHorses(BattleSideEnum.Defender, !_mapEvent.IsSiegeAssault);
        _missionAgentSpawnLogic.SetSpawnHorses(BattleSideEnum.Attacker, !_mapEvent.IsSiegeAssault);

        var sizing = ReadSizing();

        if (sizing.Ready)
        {
            if (sizing.SizeNow)
            {
                // On-time (common): both sides of one complete grant are present, so size before the first tick.
                RunJointInit(sizing);
                _sized = true;
                Logger.Information("[BattleSync] Coop spawn sized on start: Defender={DefInitial}/{DefTotal}, Attacker={AtkInitial}/{AtkTotal}",
                    sizing.DefenderInitial, sizing.DefenderOwned, sizing.AttackerInitial, sizing.AttackerOwned);
                return;
            }

            // Both authoritative responses arrived but neither contains a combatant. There is no valid 0/0
            // spawn setup (Init divides the joint cap by the total), and latching IsSized would open an empty
            // deployment forever. Keep a safe held phase until the timeout ends the mission normally.
            AddHeldPhases();
            Logger.Error("[BattleSync] Both battle reserves arrived empty; holding briefly before aborting the invalid mission");
            return;
        }

        // A reserve is still in flight — hold both sides at zero until OnMissionTick sizes them (or the deadline).
        AddHeldPhases();
        Logger.Warning("[BattleSync] Coop spawn handler started before reserves arrived (Def populated={Def}, Atk populated={Atk}) — sizing on tick once both land",
            sizing.DefenderPopulated, sizing.AttackerPopulated);
    }

    // Size once both suppliers populate, then latch. If a reserve never lands, size a usable partial response
    // after ReserveHoldDeadlineSeconds; if no combatant exists, end the invalid mission instead. A mid-battle
    // migration re-feed re-populates an already-sized supplier and is left to ReinforcementFielder, which can
    // distinguish newly-owned parties with no adopted live agents without disturbing the initial phase sizing.
    public override void OnMissionTick(float dt)
    {
        if (_sized && !_emptyBattleAbortRequested)
            ReconcileInitialPhaseScopes();

        base.OnMissionTick(dt);
        if (_sized || _emptyBattleAbortRequested) return;

        _heldSeconds += dt;
        var sizing = ReadSizing();
        if (ShouldContinueHolding(sizing)) return;

        if (!sizing.HasAnyOwnedTroops)
        {
            AbortEmptyBattle(sizing);
            return;
        }

        AcceptMissingReserveSides(sizing);

        // Ready, or the deadline expired with a partial/missing reserve. At least one combatant exists here,
        // so the joint Init cannot hit its invalid 0/0 split.
        RunJointInit(sizing);
        LogSizingCompleted(sizing);
        _sized = true;
    }

    private bool ShouldContinueHolding(SideSizing sizing)
    {
        return _heldSeconds < ReserveHoldDeadlineSeconds
            && (!sizing.Ready || !sizing.HasAnyOwnedTroops);
    }

    // A zero-troop mission has no safe sizing or deployment path. Reserves had the full hold window to
    // arrive/recover; terminate through Mission.EndMission so the attached coop lifecycle releases the
    // spawn gate and returns to campaign instead of leaving the loading screen gated forever.
    private void AbortEmptyBattle(SideSizing sizing)
    {
        _emptyBattleAbortRequested = true;
        Logger.Error("[BattleSync] No battle troops arrived within {Sec}s (Def populated={DefP}, Atk populated={AtkP}); ending invalid mission",
            ReserveHoldDeadlineSeconds, sizing.DefenderPopulated, sizing.AttackerPopulated);
        base.Mission.EndMission();
    }

    // This is the one point where an empty side becomes intentional rather than merely late. Record exactly
    // which reserve timed out so the controller can eventually release BattleEndLogic and the depletion patch
    // can call only that side depleted; the populated side must still field an agent.
    private static void AcceptMissingReserveSides(SideSizing sizing)
    {
        if (sizing.Ready) return;
        if (!sizing.DefenderIncluded)
            BattleSpawnGate.AcceptMissingReserveSide(BattleSideEnum.Defender);
        if (!sizing.AttackerIncluded)
            BattleSpawnGate.AcceptMissingReserveSide(BattleSideEnum.Attacker);
    }

    private static void LogSizingCompleted(SideSizing sizing)
    {
        if (sizing.Ready)
            Logger.Information("[BattleSync] Reserves landed after start; sized sides jointly: Defender={DefInitial}/{DefTotal}, Attacker={AtkInitial}/{AtkTotal}",
                sizing.DefenderInitialForSizing, sizing.DefenderOwnedForSizing,
                sizing.AttackerInitialForSizing, sizing.AttackerOwnedForSizing);
        else
            Logger.Warning("[BattleSync] Reserves incomplete after {Sec}s hold (Def populated={DefP}/generation={DefGen}, Atk populated={AtkP}/generation={AtkGen}) — sizing only the newest coherent grant data: Defender={DefInitial}/{DefTotal}, Attacker={AtkInitial}/{AtkTotal}",
                ReserveHoldDeadlineSeconds, sizing.DefenderPopulated, sizing.DefenderGrantGeneration,
                sizing.AttackerPopulated, sizing.AttackerGrantGeneration,
                sizing.DefenderInitialForSizing, sizing.DefenderOwnedForSizing,
                sizing.AttackerInitialForSizing, sizing.AttackerOwnedForSizing);
    }

    // Snapshot both suppliers under their locks so a network-thread reserve refresh cannot mix one revision's
    // total with another revision's initial entitlement. Shared by AfterStart and OnMissionTick.
    private SideSizing ReadSizing()
    {
        CoopTroopSupplier.GetSizingSnapshots(
            _defenderSupplier,
            _attackerSupplier,
            out var defender,
            out var attacker);

        return new SideSizing(
            defender.IsPopulated,
            attacker.IsPopulated,
            defender.TotalTroops,
            attacker.TotalTroops,
            defender.InitialTroops,
            attacker.InitialTroops,
            defender.GrantGeneration,
            attacker.GrantGeneration,
            defender.CompletesInitialSizing,
            attacker.CompletesInitialSizing,
            defender.PartyCapacities,
            attacker.PartyCapacities,
            defender.ReserveRevision,
            attacker.ReserveRevision);
    }

    // Full totals retain each party's reinforcement depth. The server-assigned initial counts already share one
    // global budget, so FreeAllocation keeps them exact instead of applying a separate cap on every client.
    private void RunJointInit(SideSizing sizing)
    {
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Defender].Clear();
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Attacker].Clear();

        if (sizing.DefenderIncluded)
        {
            _defenderSupplier.BeginInitialSupply(sizing.DefenderPartyCapacities);
            _defenderSizedReserveRevision = sizing.DefenderReserveRevision;
        }
        if (sizing.AttackerIncluded)
        {
            _attackerSupplier.BeginInitialSupply(sizing.AttackerPartyCapacities);
            _attackerSizedReserveRevision = sizing.AttackerReserveRevision;
        }

        var settings = CreateCoopBattleWaveSpawnSettings();
        _missionAgentSpawnLogic.InitWithSinglePhase(
            sizing.DefenderOwnedForSizing,
            sizing.AttackerOwnedForSizing,
            sizing.DefenderInitialForSizing,
            sizing.AttackerInitialForSizing,
            spawnDefenders: true, spawnAttackers: true, in settings);

        if (sizing.DefenderIncluded)
            _defenderSupplier.RecordPhaseCapacities(sizing.DefenderPartyCapacities);
        if (sizing.AttackerIncluded)
            _attackerSupplier.RecordPhaseCapacities(sizing.AttackerPartyCapacities);

        // Init leaves both sides spawn-active; the native path clears them after Init but nothing does here, so
        // restore it — else SetupTeams's first side spawns both at once and the per-side freeze misses one.
        _missionAgentSpawnLogic.SetSpawnTroops(BattleSideEnum.Defender, spawnTroops: false);
        _missionAgentSpawnLogic.SetSpawnTroops(BattleSideEnum.Attacker, spawnTroops: false);
    }

    // A BR-033 ownership refresh can shrink the reserve after Init but before native deployment pulls it.
    // Rebase the one native phase before CheckDeployment so it never waits for a lease the supplier lost.
    private void ReconcileInitialPhaseScopes()
    {
        if (_missionAgentSpawnLogic == null || _missionAgentSpawnLogic.IsInitialSpawnOver) return;

        bool defenderChanged = ReconcileInitialPhaseScope(
            BattleSideEnum.Defender,
            _defenderSupplier,
            ref _defenderSizedReserveRevision);
        bool attackerChanged = ReconcileInitialPhaseScope(
            BattleSideEnum.Attacker,
            _attackerSupplier,
            ref _attackerSizedReserveRevision);
        if (!defenderChanged && !attackerChanged) return;

        var defenderPhase = _missionAgentSpawnLogic.DefenderActivePhase;
        var attackerPhase = _missionAgentSpawnLogic.AttackerActivePhase;
        if (defenderPhase != null && attackerPhase != null)
            Mission.SetBattleAgentCount(Math.Min(defenderPhase.InitialSpawnNumber, attackerPhase.InitialSpawnNumber));
        if (defenderChanged && defenderPhase != null)
            Mission.SetInitialAgentCountForSide(BattleSideEnum.Defender, defenderPhase.TotalSpawnNumber);
        if (attackerChanged && attackerPhase != null)
            Mission.SetInitialAgentCountForSide(BattleSideEnum.Attacker, attackerPhase.TotalSpawnNumber);
    }

    private bool ReconcileInitialPhaseScope(
        BattleSideEnum side,
        CoopTroopSupplier supplier,
        ref int sizedReserveRevision)
    {
        var snapshot = supplier.GetInitialPhaseSnapshot();
        if (!snapshot.IsCaptured || snapshot.ReserveRevision == sizedReserveRevision) return false;

        var phase = side == BattleSideEnum.Defender
            ? _missionAgentSpawnLogic.DefenderActivePhase
            : _missionAgentSpawnLogic.AttackerActivePhase;
        var context = _missionAgentSpawnLogic._battleSideSpawnContexts[(int)side];
        if (phase == null || context == null || phase.InitialSpawnNumber <= 0)
        {
            sizedReserveRevision = snapshot.ReserveRevision;
            return false;
        }
        if (!supplier.CommitInitialPhaseRebase(snapshot.ReserveRevision)) return false;

        var rebased = InitialPhaseSizing.Calculate(
            phase.TotalSpawnNumber,
            phase.InitialSpawnNumber,
            context._numSpawnedTroops,
            context.ReservedTroopsCount,
            snapshot.RemainingTroops,
            snapshot.RemainingInitialTroops);
        phase.TotalSpawnNumber = rebased.TotalTroops;
        phase.InitialSpawnNumber = rebased.InitialTroops;
        phase.RemainingSpawnNumber = rebased.RemainingTroops;
        _missionAgentSpawnLogic._numberOfTroopsInTotal[(int)side] = rebased.TotalTroops;
        sizedReserveRevision = snapshot.ReserveRevision;

        Logger.Information("[BattleSync] Rebased pre-deployment {Side} reserve at revision {Revision}: initial={Initial}, total={Total}",
            side, snapshot.ReserveRevision, rebased.InitialTroops, rebased.TotalTroops);
        return true;
    }

    private static MissionSpawnSettings CreateCoopBattleWaveSpawnSettings()
    {
        var settings = CreateSandBoxBattleWaveSpawnSettings();
        settings.InitialTroopsSpawnMethod = MissionSpawnSettings.InitialSpawnMethod.FreeAllocation;
        return settings;
    }

    // Zero phases so the first tick has active phases to read (else DefenderActivePhase NREs), without feeding Init
    // a 0/0 total: its float battle-size split yields NaN, which Mono casts to int.MinValue (desktop .NET gives 0).
    private void AddHeldPhases()
    {
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Defender].Add(new MissionSpawnPhase());
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Attacker].Add(new MissionSpawnPhase());
    }

    /// <summary>
    /// Snapshot of both suppliers plus the joint sizing derived from it (unit-testable — pure over its readings).
    /// Ready = both sides of the same complete server grant landed; SizeNow additionally requires a positive
    /// combined total, so Init is never handed a 0/0 battle-size split.
    /// </summary>
    public readonly struct SideSizing
    {
        public readonly bool DefenderPopulated;
        public readonly bool AttackerPopulated;
        public readonly int DefenderOwned;
        public readonly int AttackerOwned;
        public readonly int DefenderInitial;
        public readonly int AttackerInitial;
        public readonly long DefenderGrantGeneration;
        public readonly long AttackerGrantGeneration;
        public readonly bool DefenderGrantComplete;
        public readonly bool AttackerGrantComplete;
        public readonly CoopTroopSupplier.PartyCapacitySnapshot[] DefenderPartyCapacities;
        public readonly CoopTroopSupplier.PartyCapacitySnapshot[] AttackerPartyCapacities;
        public readonly int DefenderReserveRevision;
        public readonly int AttackerReserveRevision;
        public readonly bool DefenderIncluded;
        public readonly bool AttackerIncluded;

        public SideSizing(bool defenderPopulated, bool attackerPopulated, int defenderOwned, int attackerOwned,
            int defenderInitial, int attackerInitial, long defenderGrantGeneration = 0,
            long attackerGrantGeneration = 0, bool defenderGrantComplete = true,
            bool attackerGrantComplete = true,
            CoopTroopSupplier.PartyCapacitySnapshot[] defenderPartyCapacities = null,
            CoopTroopSupplier.PartyCapacitySnapshot[] attackerPartyCapacities = null,
            int defenderReserveRevision = 0,
            int attackerReserveRevision = 0)
        {
            DefenderPopulated = defenderPopulated;
            AttackerPopulated = attackerPopulated;
            DefenderOwned = defenderOwned;
            AttackerOwned = attackerOwned;
            DefenderInitial = defenderInitial;
            AttackerInitial = attackerInitial;
            DefenderGrantGeneration = defenderGrantGeneration;
            AttackerGrantGeneration = attackerGrantGeneration;
            DefenderGrantComplete = defenderGrantComplete;
            AttackerGrantComplete = attackerGrantComplete;
            DefenderPartyCapacities = defenderPartyCapacities ?? System.Array.Empty<CoopTroopSupplier.PartyCapacitySnapshot>();
            AttackerPartyCapacities = attackerPartyCapacities ?? System.Array.Empty<CoopTroopSupplier.PartyCapacitySnapshot>();
            DefenderReserveRevision = defenderReserveRevision;
            AttackerReserveRevision = attackerReserveRevision;

            // A timeout may still size a partial response, but never combine two different grants. Keep only
            // the newer side until its matching partner arrives; equal generations belong to one batch.
            bool generationsDiffer = defenderPopulated && attackerPopulated
                && defenderGrantGeneration != attackerGrantGeneration;
            DefenderIncluded = defenderPopulated
                && (!generationsDiffer || defenderGrantGeneration > attackerGrantGeneration);
            AttackerIncluded = attackerPopulated
                && (!generationsDiffer || attackerGrantGeneration > defenderGrantGeneration);
        }

        // Entry grants are intentionally incomplete; only a matching two-side election/refresh grant may size.
        public bool Ready => DefenderPopulated
            && AttackerPopulated
            && DefenderGrantComplete
            && AttackerGrantComplete
            && DefenderGrantGeneration == AttackerGrantGeneration;

        // Ready and at least one side owns troops: run the real Init (a positive sum avoids Init's 0/0 NaN).
        public bool SizeNow => Ready && DefenderOwnedForSizing + AttackerOwnedForSizing > 0;

        public int DefenderOwnedForSizing => DefenderIncluded ? DefenderOwned : 0;
        public int AttackerOwnedForSizing => AttackerIncluded ? AttackerOwned : 0;
        public int DefenderInitialForSizing => DefenderIncluded ? DefenderInitial : 0;
        public int AttackerInitialForSizing => AttackerIncluded ? AttackerInitial : 0;

        /// <summary>Whether a timeout can safely degrade to a one-sided sizing instead of empty/empty.</summary>
        public bool HasAnyOwnedTroops => DefenderOwnedForSizing + AttackerOwnedForSizing > 0;
    }

    public readonly struct InitialPhaseSizing
    {
        public readonly int TotalTroops;
        public readonly int InitialTroops;
        public readonly int RemainingTroops;

        private InitialPhaseSizing(int totalTroops, int initialTroops)
        {
            TotalTroops = Math.Max(0, totalTroops);
            InitialTroops = Math.Min(TotalTroops, Math.Max(0, initialTroops));
            RemainingTroops = TotalTroops - InitialTroops;
        }

        public static InitialPhaseSizing Calculate(
            int representedTotalTroops,
            int representedInitialTroops,
            int spawnedTroops,
            int reservedTroops,
            int unsuppliedTroops,
            int unsuppliedInitialTroops)
        {
            int resolved = Math.Max(0, spawnedTroops) + Math.Max(0, reservedTroops);
            return new InitialPhaseSizing(
                Math.Min(
                    Math.Max(0, representedTotalTroops),
                    resolved + Math.Max(0, unsuppliedTroops)),
                Math.Min(
                    Math.Max(0, representedInitialTroops),
                    resolved + Math.Max(0, unsuppliedInitialTroops)));
        }
    }
}
