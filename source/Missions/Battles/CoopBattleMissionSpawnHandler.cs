using System;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using SandBox.Missions.MissionLogics;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Coop replacement for <see cref="SandBoxBattleMissionSpawnHandler"/>: sizes each side to what THIS client's
/// supplier owns (its party, plus the AI/enemy side for the host), not the full side the native handler waits on
/// and never fills. The engine's battle-size cap and wave split are joint across both sides, so both are sized in
/// one pass once both reserves land: at <see cref="AfterStart"/> if already present, else held at zero until
/// <see cref="OnMissionTick"/> sees them, so a late side ends up identical to an on-time one.
/// </summary>
public class CoopBattleMissionSpawnHandler : SandBoxMissionSpawnHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopBattleMissionSpawnHandler>();
    internal const string InvalidPlayerReserveMessage = "Unable to start the battle because your party's troop reserve was not received. Returning to the campaign map.";

    // Hold this long for a still-in-flight reserve before sizing with whatever landed. A dropped or server-rejected
    // reserve request would otherwise never populate a supplier, and the deployment controller (which gates on
    // IsSized) would wedge on the loading screen forever. A partial response degrades to a one-sided battle; a
    // zero-troop response cannot produce a valid battle and is terminated through the normal mission lifecycle.
    private const float ReserveHoldDeadlineSeconds = 15f;

    private readonly CoopTroopSupplier _defenderSupplier;
    private readonly CoopTroopSupplier _attackerSupplier;
    private readonly IMessageBroker _messageBroker;
    private readonly BattleSideEnum _playerSide;

    // Latched once the sides are sized jointly; both are held at zero until then.
    private bool _sized;
    private long _appliedAllocationRevision;

    // Time spent holding both sides while a reserve is in flight (only accrues on the held path).
    private float _heldSeconds;
    private bool _invalidBattleAbortRequested;

    // Gated on by CoopBattleDeploymentMissionController: a game-thread latch, not the suppliers' network-thread
    // IsPopulated (which could read true mid-frame before Init has actually sized).
    public bool IsSized => _sized;

    public CoopBattleMissionSpawnHandler(CoopTroopSupplier defenderSupplier, CoopTroopSupplier attackerSupplier,
        IMessageBroker messageBroker, BattleSideEnum playerSide)
    {
        _defenderSupplier = defenderSupplier;
        _attackerSupplier = attackerSupplier;
        _messageBroker = messageBroker;
        _playerSide = playerSide;
    }

    public override void AfterStart()
    {
        _missionAgentSpawnLogic.SetSpawnHorses(BattleSideEnum.Defender, !_mapEvent.IsSiegeAssault);
        _missionAgentSpawnLogic.SetSpawnHorses(BattleSideEnum.Attacker, !_mapEvent.IsSiegeAssault);

        var sizing = ReadSizing();

        if (sizing.Ready)
        {
            if (sizing.SizeNow && HasLocalPlayerOrigin())
            {
                // On-time (common): both reserves present, so size before the first tick.
                RunJointInit(sizing);
                _sized = true;
                Logger.Information("[BattleSync] Coop spawn sized on start: Defender={Def}, Attacker={Atk}", sizing.DefenderOwned, sizing.AttackerOwned);
                return;
            }

            // Keep deployment held when the authoritative response cannot produce the local player agent.
            // OnMissionTick ends the invalid mission without allowing native SetupTeams to run.
            AddHeldPhases();
            Logger.Error("[BattleSync] Battle reserves cannot produce the local player origin; holding deployment before aborting the invalid mission");
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
        if (_sized)
            ReconcileRefreshedAllocation();

        base.OnMissionTick(dt);
        if (_sized || _invalidBattleAbortRequested) return;

        _heldSeconds += dt;
        var sizing = ReadSizing();
        if (ShouldContinueHolding(sizing)) return;

        if (!sizing.HasValidBattleSize || !HasLocalPlayerOrigin())
        {
            AbortInvalidBattle(sizing);
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

    // The native deployment controller dereferences InitialPlayerAgent after spawning. Without the local
    // player's authoritative origin, keep IsSized false and end through the attached mission lifecycle.
    private void AbortInvalidBattle(SideSizing sizing)
    {
        _invalidBattleAbortRequested = true;
        var playerPartyId = GetLocalPlayerPartyId();
        Logger.Error("[BattleSync] Local player origin missing from battle reserves (side={Side}, party={PartyId}, Def populated={DefP}, Atk populated={AtkP}); ending invalid mission",
            _playerSide, playerPartyId, sizing.DefenderPopulated, sizing.AttackerPopulated);
        _messageBroker.Publish(this, new SendInformationMessage(InvalidPlayerReserveMessage));
        base.Mission.EndMission();
    }

    private bool HasLocalPlayerOrigin()
    {
        return HasLocalPlayerOrigin(_playerSide, GetLocalPlayerPartyId(), _defenderSupplier, _attackerSupplier);
    }

    private string GetLocalPlayerPartyId()
    {
        var playerSupplier = _playerSide == BattleSideEnum.Attacker ? _attackerSupplier : _defenderSupplier;
        return playerSupplier.PlayerPartyId;
    }

    internal static bool HasLocalPlayerOrigin(BattleSideEnum playerSide, string playerPartyId,
        CoopTroopSupplier defenderSupplier, CoopTroopSupplier attackerSupplier)
    {
        var playerSupplier = playerSide == BattleSideEnum.Attacker ? attackerSupplier : defenderSupplier;
        return playerSupplier.GetRemainingForParty(playerPartyId) > 0;
    }

    // This is the one point where an empty side becomes intentional rather than merely late. Record exactly
    // which reserve timed out so the controller can eventually release BattleEndLogic and the depletion patch
    // can call only that side depleted; the populated side must still field an agent.
    private static void AcceptMissingReserveSides(SideSizing sizing)
    {
        if (sizing.Ready) return;
        if (!sizing.DefenderPopulated)
            BattleSpawnGate.AcceptMissingReserveSide(BattleSideEnum.Defender);
        if (!sizing.AttackerPopulated)
            BattleSpawnGate.AcceptMissingReserveSide(BattleSideEnum.Attacker);
    }

    private static void LogSizingCompleted(SideSizing sizing)
    {
        if (sizing.Ready)
            Logger.Information("[BattleSync] Reserves landed after start; sized sides jointly: Defender={Def}, Attacker={Atk}", sizing.DefenderOwned, sizing.AttackerOwned);
        else
            Logger.Warning("[BattleSync] Reserves incomplete after {Sec}s hold (Def populated={DefP}, Atk populated={AtkP}) — sizing with what landed: Defender={Def}, Attacker={Atk}",
                ReserveHoldDeadlineSeconds, sizing.DefenderPopulated, sizing.AttackerPopulated, sizing.DefenderOwned, sizing.AttackerOwned);
    }

    // Snapshot both suppliers into a SideSizing. Read populated before owned so the pair can't tear: SetReserve
    // commits the entries then flips populated under one lock. Shared by AfterStart and OnMissionTick.
    private SideSizing ReadSizing()
    {
        bool defenderPopulated = _defenderSupplier.IsPopulated;
        bool attackerPopulated = _attackerSupplier.IsPopulated;
        // The SIDE's totals, not this client's share of them. The engine splits a fixed battle size in
        // proportion to the two numbers it is given, so a client sizing from what it happens to own measures
        // a side that is divided between players at a fraction of its strength: its opponent gets capped
        // against that fraction, and the divided side ends up fielding more men than the larger one.
        int defenderOwned = _defenderSupplier.SideTotalTroops;
        int attackerOwned = _attackerSupplier.SideTotalTroops;
        int battleSize = ResolveBattleSize(defenderPopulated, _defenderSupplier.BattleSize,
            attackerPopulated, _attackerSupplier.BattleSize);
        return new SideSizing(defenderPopulated, attackerPopulated, defenderOwned, attackerOwned, battleSize);
    }

    internal static int ResolveBattleSize(bool defenderPopulated, int defenderBattleSize,
        bool attackerPopulated, int attackerBattleSize)
    {
        if (defenderPopulated && attackerPopulated)
            return defenderBattleSize > 0 && defenderBattleSize == attackerBattleSize ? defenderBattleSize : 0;
        if (defenderPopulated)
            return Math.Max(0, defenderBattleSize);
        if (attackerPopulated)
            return Math.Max(0, attackerBattleSize);
        return 0;
    }

    // Re-run the engine's Init with the real totals (initial == total; Init applies the joint cap, wave split and
    // agent counts). Clear the placeholder phases first — InitWithSinglePhase appends, so a leftover held phase
    // would leave two active phases. Nothing spawned while held, so no double-spawn.
    private void RunJointInit(SideSizing sizing)
    {
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Defender].Clear();
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Attacker].Clear();

        var settings = CreateSandBoxBattleWaveSpawnSettings();
        var targets = ReinforcementFielder.RecoveryTargets.Calculate(
            sizing.DefenderOwned,
            sizing.AttackerOwned,
            sizing.BattleSize,
            settings.MaximumBattleSideRatio,
            settings.DefenderAdvantageFactor);
        var authoritativeSettings = new MissionSpawnSettings(
            MissionSpawnSettings.InitialSpawnMethod.FreeAllocation,
            settings.ReinforcementTroopsTimingMethod,
            settings.ReinforcementTroopsSpawnMethod,
            settings.GlobalReinforcementInterval,
            settings.ReinforcementBatchPercentage,
            settings.DesiredReinforcementPercentage,
            settings.ReinforcementWavePercentage,
            settings.MaximumReinforcementWaveCount,
            settings.DefenderReinforcementBatchPercentage,
            settings.AttackerReinforcementBatchPercentage,
            settings.DefenderAdvantageFactor,
            settings.MaximumBattleSideRatio);
        _missionAgentSpawnLogic.InitWithSinglePhase(sizing.DefenderOwned, sizing.AttackerOwned,
            targets.Defenders, targets.Attackers, spawnDefenders: true, spawnAttackers: true,
            in authoritativeSettings);

        GuaranteePlayerInitialSlots();
        ClampPhasesToOwnedShare(BattleSideEnum.Defender, _defenderSupplier);
        ClampPhasesToOwnedShare(BattleSideEnum.Attacker, _attackerSupplier);

        // Init leaves both sides spawn-active; the native path clears them after Init but nothing does here, so
        // restore it — else SetupTeams's first side spawns both at once and the per-side freeze misses one.
        _missionAgentSpawnLogic.SetSpawnTroops(BattleSideEnum.Defender, spawnTroops: false);
        _missionAgentSpawnLogic.SetSpawnTroops(BattleSideEnum.Attacker, spawnTroops: false);
        var defenderSnapshot = _defenderSupplier.CaptureAllocationSnapshot();
        var attackerSnapshot = _attackerSupplier.CaptureAllocationSnapshot();
        _appliedAllocationRevision = MatchingAllocationRevision(defenderSnapshot.Revision, attackerSnapshot.Revision);
    }

    internal BattleSizeState CaptureBattleSizeState()
    {
        SideSizing sizing = ReadSizing();
        var settings = CreateSandBoxBattleWaveSpawnSettings();
        var targets = ReinforcementFielder.RecoveryTargets.Calculate(
            sizing.DefenderOwned,
            sizing.AttackerOwned,
            sizing.BattleSize,
            settings.MaximumBattleSideRatio,
            settings.DefenderAdvantageFactor);
        var defenderSnapshot = _defenderSupplier.CaptureAllocationSnapshot();
        var attackerSnapshot = _attackerSupplier.CaptureAllocationSnapshot();

        return new BattleSizeState(
            _sized,
            sizing.DefenderOwned,
            sizing.AttackerOwned,
            sizing.BattleSize,
            targets.Defenders,
            targets.Attackers,
            MatchingAllocationRevision(defenderSnapshot.Revision, attackerSnapshot.Revision));
    }

    // Reserve refreshes are sent as a reliable-ordered pair. Wait until both suppliers advanced, then resize
    // only the unspent lifetime quota; InitialSpawnNumber/InitialSpawnedNumber keep deployment one-shot.
    private void ReconcileRefreshedAllocation()
    {
        var defenderSnapshot = _defenderSupplier.CaptureAllocationSnapshot();
        var attackerSnapshot = _attackerSupplier.CaptureAllocationSnapshot();
        long allocationRevision = MatchingAllocationRevision(defenderSnapshot.Revision, attackerSnapshot.Revision);
        if (allocationRevision <= _appliedAllocationRevision
            || defenderSnapshot.BattleSize <= 0
            || defenderSnapshot.BattleSize != attackerSnapshot.BattleSize)
            return;

        BattleSpawnGate.RestoreReserveSide(BattleSideEnum.Defender);
        BattleSpawnGate.RestoreReserveSide(BattleSideEnum.Attacker);

        var settings = _missionAgentSpawnLogic.SpawnSettings;
        var targets = ReinforcementFielder.RecoveryTargets.Calculate(
            defenderSnapshot.SideTotalTroops,
            attackerSnapshot.SideTotalTroops,
            defenderSnapshot.BattleSize,
            settings.MaximumBattleSideRatio,
            settings.DefenderAdvantageFactor);

        ReconcileSideLifetimeQuota(BattleSideEnum.Defender, defenderSnapshot, targets.Defenders, settings);
        ReconcileSideLifetimeQuota(BattleSideEnum.Attacker, attackerSnapshot, targets.Attackers, settings);

        _appliedAllocationRevision = allocationRevision;
        Logger.Information("[BattleSync] Reconciled refreshed native quotas: Defender={Def}, Attacker={Atk}",
            _missionAgentSpawnLogic.DefenderActivePhase.TotalSpawnNumber,
            _missionAgentSpawnLogic.AttackerActivePhase.TotalSpawnNumber);
    }

    internal static long MatchingAllocationRevision(long defenderRevision, long attackerRevision)
        => defenderRevision > 0 && defenderRevision == attackerRevision ? defenderRevision : 0;

    private void ReconcileSideLifetimeQuota(BattleSideEnum side,
        CoopTroopSupplier.AllocationSnapshot allocationSnapshot, int initialTarget, MissionSpawnSettings settings)
    {
        int sideLifetimeTarget = CalculateLifetimeTarget(
            allocationSnapshot.SideTotalTroops,
            initialTarget,
            settings.ReinforcementWavePercentage,
            settings.MaximumReinforcementWaveCount);
        int ownedLifetimeTarget = allocationSnapshot.OwnedShareOf(sideLifetimeTarget);
        int reserved = _missionAgentSpawnLogic._battleSideSpawnContexts[(int)side].ReservedTroopsCount;
        ReconcilePhaseLifetimeQuota(
            _missionAgentSpawnLogic._phases[(int)side][0],
            ownedLifetimeTarget,
            allocationSnapshot.SuppliedTroops,
            reserved);
        _missionAgentSpawnLogic._numberOfTroopsInTotal[(int)side] = ownedLifetimeTarget;
    }

    internal static int CalculateLifetimeTarget(int sideTotal, int initialTarget, float wavePercentage,
        int maximumWaveCount)
    {
        initialTarget = Math.Min(Math.Max(0, initialTarget), Math.Max(0, sideTotal));
        int remaining = Math.Max(0, sideTotal - initialTarget);
        if (maximumWaveCount > 0)
        {
            int waveSize = Math.Max(1, (int)(initialTarget * wavePercentage));
            remaining = Math.Min(remaining, waveSize * maximumWaveCount);
        }
        return initialTarget + remaining;
    }

    internal static void ReconcilePhaseLifetimeQuota(MissionSpawnPhase phase, int refreshedOwnedTarget,
        int supplied, int reserved)
    {
        if (phase == null) return;

        int nativeSpawned = Math.Max(0, phase.TotalSpawnNumber - phase.RemainingSpawnNumber);
        int consumedSupply = Math.Max(0, supplied - reserved);
        int committed = Math.Max(nativeSpawned, consumedSupply);
        int remaining = Math.Max(0, refreshedOwnedTarget - committed);
        phase.RemainingSpawnNumber = remaining;
        phase.TotalSpawnNumber = committed + remaining;
    }

    private void GuaranteePlayerInitialSlots()
    {
        var defender = _missionAgentSpawnLogic.DefenderActivePhase;
        var attacker = _missionAgentSpawnLogic.AttackerActivePhase;
        AdjustInitialAllocations(
            defender.InitialSpawnNumber,
            attacker.InitialSpawnNumber,
            defender.TotalSpawnNumber,
            attacker.TotalSpawnNumber,
            _defenderSupplier.PlayerOwnedPartyCount,
            _attackerSupplier.PlayerOwnedPartyCount,
            out var defenderInitial,
            out var attackerInitial);
        defender.InitialSpawnNumber = defenderInitial;
        defender.RemainingSpawnNumber = defender.TotalSpawnNumber - defenderInitial;
        attacker.InitialSpawnNumber = attackerInitial;
        attacker.RemainingSpawnNumber = attacker.TotalSpawnNumber - attackerInitial;
    }

    internal static void AdjustInitialAllocations(
        int defenderInitial,
        int attackerInitial,
        int defenderTotal,
        int attackerTotal,
        int defenderPlayers,
        int attackerPlayers,
        out int adjustedDefenders,
        out int adjustedAttackers)
    {
        adjustedDefenders = defenderInitial;
        adjustedAttackers = attackerInitial;
        int defenderMinimum = Math.Min(defenderPlayers, defenderTotal);
        int attackerMinimum = Math.Min(attackerPlayers, attackerTotal);

        int transfer = Math.Min(Math.Max(0, defenderMinimum - adjustedDefenders),
            Math.Max(0, adjustedAttackers - attackerMinimum));
        adjustedDefenders += transfer;
        adjustedAttackers -= transfer;

        transfer = Math.Min(Math.Max(0, attackerMinimum - adjustedAttackers),
            Math.Max(0, adjustedDefenders - defenderMinimum));
        adjustedAttackers += transfer;
        adjustedDefenders -= transfer;
    }

    // Init uses whole-side totals for vanilla sizing, but this client's deployment target must be its exact
    // owner share. Derive remaining from total and initial so the native phase invariant stays intact.
    private void ClampPhasesToOwnedShare(BattleSideEnum side, CoopTroopSupplier supplier)
    {
        foreach (var phase in _missionAgentSpawnLogic._phases[(int)side])
        {
            AdjustPhaseToOwnedShare(
                phase.TotalSpawnNumber,
                phase.InitialSpawnNumber,
                supplier.OwnedShareOf(phase.TotalSpawnNumber),
                supplier.OwnedShareOf(phase.InitialSpawnNumber),
                out var total,
                out var initial,
                out var remaining);
            phase.TotalSpawnNumber = total;
            phase.InitialSpawnNumber = initial;
            phase.RemainingSpawnNumber = remaining;
        }
    }

    internal static void AdjustPhaseToOwnedShare(
        int sideTotal,
        int sideInitial,
        int ownedTotal,
        int ownedInitial,
        out int total,
        out int initial,
        out int remaining)
    {
        total = ReachableSpawnNumber(sideTotal, ownedTotal);
        initial = Math.Min(total, ReachableSpawnNumber(sideInitial, ownedInitial));
        remaining = total - initial;
    }

    private static int ReachableSpawnNumber(int sideNumber, CoopTroopSupplier supplier)
        => ReachableSpawnNumber(sideNumber, supplier.OwnedShareOf(sideNumber));

    /// <summary>
    /// The largest spawn target this client can actually reach: never more than the side needs, and never more
    /// than the supplier will hand over when asked for that many.
    /// </summary>
    internal static int ReachableSpawnNumber(int sideNumber, int ownedShareOfSideNumber)
        => Math.Min(sideNumber, ownedShareOfSideNumber);

    // Zero phases so the first tick has active phases to read (else DefenderActivePhase NREs), without feeding Init
    // a 0/0 total: its float battle-size split yields NaN, which Mono casts to int.MinValue (desktop .NET gives 0).
    private void AddHeldPhases()
    {
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Defender].Add(new MissionSpawnPhase());
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Attacker].Add(new MissionSpawnPhase());
    }

    /// <summary>
    /// Snapshot of both suppliers plus the joint sizing derived from it (unit-testable — pure over its readings).
    /// Ready = both reserves landed; SizeNow additionally requires a positive combined total, so Init is never
    /// handed a 0/0 battle-size split.
    /// </summary>
    public readonly struct SideSizing
    {
        public readonly bool DefenderPopulated;
        public readonly bool AttackerPopulated;
        public readonly int DefenderOwned;
        public readonly int AttackerOwned;
        public readonly int BattleSize;

        public SideSizing(bool defenderPopulated, bool attackerPopulated, int defenderOwned, int attackerOwned,
            int battleSize)
        {
            DefenderPopulated = defenderPopulated;
            AttackerPopulated = attackerPopulated;
            DefenderOwned = defenderOwned;
            AttackerOwned = attackerOwned;
            BattleSize = battleSize;
        }

        // Both reserves landed: commit the joint sizing now (else keep holding both sides at zero).
        public bool Ready => DefenderPopulated && AttackerPopulated;

        // Ready and at least one side owns troops: run the real Init (a positive sum avoids Init's 0/0 NaN).
        public bool SizeNow => Ready && DefenderOwned + AttackerOwned > 0 && BattleSize > 0;

        public bool HasValidBattleSize => BattleSize > 0;

        /// <summary>Whether a timeout can safely degrade to a one-sided sizing instead of empty/empty.</summary>
        public bool HasAnyOwnedTroops => DefenderOwned + AttackerOwned > 0;
    }

    internal readonly struct BattleSizeState
    {
        public readonly bool IsSized;
        public readonly int DefenderTotal;
        public readonly int AttackerTotal;
        public readonly int BattleSize;
        public readonly int DefenderTarget;
        public readonly int AttackerTarget;
        public readonly long AllocationRevision;

        public BattleSizeState(
            bool isSized,
            int defenderTotal,
            int attackerTotal,
            int battleSize,
            int defenderTarget,
            int attackerTarget,
            long allocationRevision)
        {
            IsSized = isSized;
            DefenderTotal = defenderTotal;
            AttackerTotal = attackerTotal;
            BattleSize = battleSize;
            DefenderTarget = defenderTarget;
            AttackerTarget = attackerTarget;
            AllocationRevision = allocationRevision;
        }
    }
}
