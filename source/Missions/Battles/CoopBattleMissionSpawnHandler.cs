using Common.Logging;
using GameInterface.Services.MapEvents.TroopSupply;
using SandBox.Missions.MissionLogics;
using Serilog;
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
/// Reserves arrive on a separate network round trip from the mission-open message (both sides in one batch), so
/// they can land after the mission is built. Because the engine's battle-size cap and reinforcement-wave split
/// are JOINT — one side's initial budget depends on the other side's total — this sizes both sides in a single
/// pass, and only once BOTH reserves are populated. If both are ready at <see cref="AfterStart"/> (the on-time
/// path) it sizes immediately; otherwise both sides are held at zero (a zero-initial phase spawns nothing) until
/// <see cref="OnMissionTick"/> sees both land and runs the engine's own Init once — so a late side ends up
/// identical to an on-time one (same cap, same waves, same agent counts). The deployment controller decides WHEN
/// the sized troops spawn (frozen during deployment, released on Start Battle).
/// </para>
/// </summary>
public class CoopBattleMissionSpawnHandler : SandBoxMissionSpawnHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopBattleMissionSpawnHandler>();

    private readonly CoopTroopSupplier _defenderSupplier;
    private readonly CoopTroopSupplier _attackerSupplier;

    // The joint battle-size cap and reinforcement-wave split depend on BOTH sides' totals, so we size once, when
    // both reserves have landed. Until then both sides are held at zero (a zero-initial phase spawns nothing) and
    // this stays false; OnMissionTick runs the single joint Init the moment both are populated, then latches.
    private bool _sized;

    // Set on the game thread right after the joint sizing commits. CoopBattleDeploymentMissionController gates its
    // one-time team/command setup on this so that setup runs only once the sides are sized. A raw supplier
    // IsPopulated check wouldn't be safe there: SetReserve flips populated on the network thread, so it can read
    // true between this handler's tick and the controller's tick in the same frame, before Init has sized.
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

        // Read populated before owned so the pair can't tear: SetReserve commits a side's entries and only then
        // flips populated (both under one lock), so once populated reads true a later TotalTroops read sees them.
        bool defenderPopulated = _defenderSupplier.IsPopulated;
        bool attackerPopulated = _attackerSupplier.IsPopulated;
        int defenderOwned = _defenderSupplier.TotalTroops;
        int attackerOwned = _attackerSupplier.TotalTroops;
        var decision = DecideJointSizing(defenderPopulated, attackerPopulated, defenderOwned, attackerOwned);

        if (decision.Ready)
        {
            // On-time (the common path): both reserves landed during scene load, so let the engine's own Init do
            // the battle-size cap, wave staging and agent-count setup before the first tick.
            if (decision.SizeNow)
                RunJointInit(defenderOwned, attackerOwned);
            else
                AddHeldPhases(); // both sides own nothing (defensive): keep phases so ticks don't NRE, spawn nothing
            _sized = true;
            Logger.Information("[BattleSync] Coop spawn sized on start: Defender={Def}, Attacker={Atk}", defenderOwned, attackerOwned);
            return;
        }

        // A reserve is still in flight. Hold BOTH sides at zero (so neither spawns against a not-yet-known enemy
        // total) with harmless zero phases the first CheckDeployment tick can read; OnMissionTick sizes once both land.
        AddHeldPhases();
        Logger.Warning("[BattleSync] Coop spawn handler started before reserves arrived (Def populated={Def}, Atk populated={Atk}) — sizing on tick once both land",
            defenderPopulated, attackerPopulated);
    }

    // Once both suppliers are populated, run the single joint Init. Runs on the game thread (after AfterStart, so
    // placeholder phases exist), reading the suppliers' lock-guarded getters that the network-thread SetReserve
    // writes — no callback or marshaling. Latches after the first sizing; a mid-battle migration re-feed (which
    // re-populates an already-sized supplier) is left to the adopt/puppet path.
    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        if (_sized) return;

        // Read populated before owned, same reason as AfterStart: reading owned first could tear and latch a
        // populated side as empty, stranding its troops because _sized then blocks any re-read.
        bool defenderPopulated = _defenderSupplier.IsPopulated;
        bool attackerPopulated = _attackerSupplier.IsPopulated;
        int defenderOwned = _defenderSupplier.TotalTroops;
        int attackerOwned = _attackerSupplier.TotalTroops;
        var decision = DecideJointSizing(defenderPopulated, attackerPopulated, defenderOwned, attackerOwned);
        if (!decision.Ready) return;

        if (decision.SizeNow)
        {
            RunJointInit(defenderOwned, attackerOwned);
            Logger.Information("[BattleSync] Reserves landed after start; sized sides jointly: Defender={Def}, Attacker={Atk}", defenderOwned, attackerOwned);
        }
        _sized = true;
    }

    // Size both sides in one shot by re-running the engine's own Init with the real owned totals (initial == total;
    // Init applies the joint battle-size cap and moves the excess into wave-staged RemainingSpawnNumber, and sets
    // the Mission agent counts). Clearing the (empty or placeholder) phases and running totals first is required
    // because InitWithSinglePhase APPENDS a phase per side — a leftover placeholder would leave two active phases.
    // Nothing has spawned while the sides were held at zero, so this cannot double-spawn.
    private void RunJointInit(int defenderOwned, int attackerOwned)
    {
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Defender].Clear();
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Attacker].Clear();
        _missionAgentSpawnLogic._numberOfTroopsInTotal[(int)BattleSideEnum.Defender] = 0;
        _missionAgentSpawnLogic._numberOfTroopsInTotal[(int)BattleSideEnum.Attacker] = 0;

        var settings = CreateSandBoxBattleWaveSpawnSettings();
        _missionAgentSpawnLogic.InitWithSinglePhase(defenderOwned, attackerOwned, defenderOwned, attackerOwned, spawnDefenders: true, spawnAttackers: true, in settings);
    }

    // Zero phases so the first CheckDeployment tick has active phases to read (else DefenderActivePhase NREs)
    // without calling Init on a 0/0 total, whose battle-size split divides by zero and (via MathF.Ceiling on NaN
    // under Mono) yields a negative agent-count sentinel.
    private void AddHeldPhases()
    {
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Defender].Add(new MissionSpawnPhase());
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Attacker].Add(new MissionSpawnPhase());
    }

    /// <summary>
    /// Pure sizing decision, split out so it is unit-testable without a live mission. <see cref="JointSizingDecision.Ready"/>
    /// is true once both reserves have landed (both suppliers populated) — the point at which both totals are final and the
    /// joint cap can be computed. <see cref="JointSizingDecision.SizeNow"/> additionally requires a positive combined total,
    /// so we never hand the engine's Init a 0/0 battle-size split.
    /// </summary>
    public static JointSizingDecision DecideJointSizing(bool defenderPopulated, bool attackerPopulated, int defenderOwned, int attackerOwned)
    {
        bool ready = defenderPopulated && attackerPopulated;
        return new JointSizingDecision(ready, ready && defenderOwned + attackerOwned > 0);
    }

    public readonly struct JointSizingDecision
    {
        // Both reserves have landed: commit the joint sizing now (else keep holding both sides at zero).
        public readonly bool Ready;
        // Ready AND at least one side owns troops: run the real Init (a positive sum avoids Init's 0/0 NaN).
        public readonly bool SizeNow;

        public JointSizingDecision(bool ready, bool sizeNow)
        {
            Ready = ready;
            SizeNow = sizeNow;
        }
    }
}
