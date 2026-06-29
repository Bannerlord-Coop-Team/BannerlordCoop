using Common.Logging;
using GameInterface.Services.MapEvents.TroopSupply;
using SandBox.Missions.MissionLogics;
using Serilog;
using TaleWorlds.Core;

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
/// Relies on the reserve being fed before the mission starts (bundled into the mission-start message), so the
/// suppliers are populated by <see cref="AfterStart"/>. Initial == total (no staged reinforcement waves); the
/// deployment controller decides WHEN the sized troops spawn (frozen during deployment, released on Start Battle).
/// </para>
/// </summary>
public class CoopBattleMissionSpawnHandler : SandBoxMissionSpawnHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopBattleMissionSpawnHandler>();

    private readonly CoopTroopSupplier _defenderSupplier;
    private readonly CoopTroopSupplier _attackerSupplier;

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
            Logger.Warning("[BattleSync] Coop spawn handler started before reserves arrived (Def populated={Def}, Atk populated={Atk}) — sizing may be wrong",
                _defenderSupplier.IsPopulated, _attackerSupplier.IsPopulated);

        _missionAgentSpawnLogic.SetSpawnHorses(BattleSideEnum.Defender, !_mapEvent.IsSiegeAssault);
        _missionAgentSpawnLogic.SetSpawnHorses(BattleSideEnum.Attacker, !_mapEvent.IsSiegeAssault);

        // initial == total: size the whole owned set as one (no staged reinforcement waves). The deployment
        // controller gates when these actually spawn — frozen in formation during deployment, released on
        // Start Battle (FinishDeployment).
        var settings = CreateSandBoxBattleWaveSpawnSettings();
        _missionAgentSpawnLogic.InitWithSinglePhase(defenderOwned, attackerOwned, defenderOwned, attackerOwned, spawnDefenders: true, spawnAttackers: true, in settings);

        Logger.Information("[BattleSync] Coop spawn sized to owned counts: Defender={Def}, Attacker={Atk}", defenderOwned, attackerOwned);
    }
}
