using Common.Logging;
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

    private readonly CoopTroopSupplier _defenderSupplier;
    private readonly CoopTroopSupplier _attackerSupplier;

    // Latched once the sides are sized jointly; both are held at zero until then.
    private bool _sized;

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

        // Read populated before owned: SetReserve sets the entries then flips populated under one lock.
        bool defenderPopulated = _defenderSupplier.IsPopulated;
        bool attackerPopulated = _attackerSupplier.IsPopulated;
        int defenderOwned = _defenderSupplier.TotalTroops;
        int attackerOwned = _attackerSupplier.TotalTroops;
        var decision = DecideJointSizing(defenderPopulated, attackerPopulated, defenderOwned, attackerOwned);

        if (decision.Ready)
        {
            // On-time (common): both reserves present, so size via Init before the first tick.
            if (decision.SizeNow)
                RunJointInit(defenderOwned, attackerOwned);
            else
                AddHeldPhases(); // both sides own nothing: keep phases so ticks don't NRE, spawn nothing
            _sized = true;
            Logger.Information("[BattleSync] Coop spawn sized on start: Defender={Def}, Attacker={Atk}", defenderOwned, attackerOwned);
            return;
        }

        // A reserve is still in flight — hold both sides at zero until OnMissionTick sizes them.
        AddHeldPhases();
        Logger.Warning("[BattleSync] Coop spawn handler started before reserves arrived (Def populated={Def}, Atk populated={Atk}) — sizing on tick once both land",
            defenderPopulated, attackerPopulated);
    }

    // Run the single joint Init once both suppliers populate, then latch. A mid-battle migration re-feed
    // re-populates an already-sized supplier and is left to the adopt path.
    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        if (_sized) return;

        // Populated before owned, as in AfterStart.
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

    // Re-run the engine's Init with the real totals (initial == total; Init applies the joint cap, wave split and
    // agent counts). Clear the placeholder phases/totals first — InitWithSinglePhase appends. Nothing spawned while
    // held, so no double-spawn.
    private void RunJointInit(int defenderOwned, int attackerOwned)
    {
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Defender].Clear();
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Attacker].Clear();
        _missionAgentSpawnLogic._numberOfTroopsInTotal[(int)BattleSideEnum.Defender] = 0;
        _missionAgentSpawnLogic._numberOfTroopsInTotal[(int)BattleSideEnum.Attacker] = 0;

        var settings = CreateSandBoxBattleWaveSpawnSettings();
        _missionAgentSpawnLogic.InitWithSinglePhase(defenderOwned, attackerOwned, defenderOwned, attackerOwned, spawnDefenders: true, spawnAttackers: true, in settings);

        // Init leaves both sides spawn-active; the native path clears them after Init but nothing does here, so
        // restore it — else SetupTeams's first side spawns both at once and the per-side freeze misses one.
        _missionAgentSpawnLogic.SetSpawnTroops(BattleSideEnum.Defender, spawnTroops: false);
        _missionAgentSpawnLogic.SetSpawnTroops(BattleSideEnum.Attacker, spawnTroops: false);
    }

    // Zero phases so the first tick has active phases to read (else DefenderActivePhase NREs), without feeding Init
    // a 0/0 total (its battle-size split divides by zero, giving a negative sentinel under Mono).
    private void AddHeldPhases()
    {
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Defender].Add(new MissionSpawnPhase());
        _missionAgentSpawnLogic._phases[(int)BattleSideEnum.Attacker].Add(new MissionSpawnPhase());
    }

    /// <summary>
    /// Pure sizing decision (unit-testable). Ready = both reserves landed; SizeNow additionally requires a positive
    /// combined total, so Init is never handed a 0/0 battle-size split.
    /// </summary>
    public static JointSizingDecision DecideJointSizing(bool defenderPopulated, bool attackerPopulated, int defenderOwned, int attackerOwned)
    {
        bool ready = defenderPopulated && attackerPopulated;
        return new JointSizingDecision(ready, ready && defenderOwned + attackerOwned > 0);
    }

    public readonly struct JointSizingDecision
    {
        // Both reserves landed: commit the joint sizing now (else keep holding both sides at zero).
        public readonly bool Ready;
        // Ready and at least one side owns troops: run the real Init (a positive sum avoids Init's 0/0 NaN).
        public readonly bool SizeNow;

        public JointSizingDecision(bool ready, bool sizeNow)
        {
            Ready = ready;
            SizeNow = sizeNow;
        }
    }
}
