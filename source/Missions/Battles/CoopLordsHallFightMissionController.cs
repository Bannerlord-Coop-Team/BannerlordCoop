using GameInterface.Services.MapEvents.TroopSupply;
using Missions.Hideouts;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;

namespace Missions.Battles;

/// <summary>
/// Vanilla's lord's hall controller fed by coop troop suppliers. Each client spawns only the troops it owns, so the
/// first spawn waits for this client's reserves and host session, every owned attacker enters at once because the
/// keep roster already holds vanilla's attacker limit, and a side is depleted by the live agents every client sees.
/// </summary>
internal sealed class CoopLordsHallFightMissionController : LordsHallFightMissionController, IMissionAgentSpawnLogic
{
    private readonly CoopTroopSupplier defenderSupplier;
    private readonly CoopTroopSupplier attackerSupplier;
    private readonly bool[] sideHadAgents = new bool[2];
    private CoopBattleController battle;

    public CoopLordsHallFightMissionController(CoopTroopSupplier defenderSupplier, CoopTroopSupplier attackerSupplier,
        BattleSideEnum playerSide)
        : base(new IMissionTroopSupplier[] { defenderSupplier, attackerSupplier },
            Campaign.Current.Models.SiegeLordsHallFightModel.AreaLostRatio,
            Campaign.Current.Models.SiegeLordsHallFightModel.AttackerDefenderTroopCountRatio,
            Campaign.Current.Models.SiegeLordsHallFightModel.MaxAttackerSideTroopCount,
            Campaign.Current.Models.SiegeLordsHallFightModel.MaxDefenderSideTroopCount,
            playerSide)
    {
        this.defenderSupplier = defenderSupplier;
        this.attackerSupplier = attackerSupplier;
    }

    public override void AfterStart()
    {
        base.AfterStart();
        battle = Mission.GetMissionBehavior<CoopBattleController>();
    }

    public override void OnMissionTick(float dt)
    {
        if (!_isMissionInitialized && !IsReadyToSpawn()) return;

        base.OnMissionTick(dt);

        // Native sizes the first attacker wave from the defenders this client spawned, which is none without the
        // host's defenders, so the rest of this client's chosen squad follows here.
        if (_isMissionInitialized && attackerSupplier.NumTroopsNotSupplied > 0)
            _missionSides[(int)BattleSideEnum.Attacker].SpawnTroops(attackerSupplier.NumTroopsNotSupplied,
                isReinforcement: false);
    }

    // Spawning before the reserves arrive would place nothing and never retry, as native initializes only once.
    private bool IsReadyToSpawn() =>
        battle != null && battle.Session.HasInstance && battle.Session.HostEpoch > 0 &&
        CoopHideoutMissionLogic.ReservesReady(battle.Session.IsLocalHost, attackerSupplier, defenderSupplier);

    // Native counts only the agents this client spawned, so a side fielded by another client would read as beaten.
    public new bool IsSideDepleted(BattleSideEnum side)
    {
        if (!_isMissionInitialized || (side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender))
            return false;

        if (Mission.Teams.Any(team => team.Side == side && team.ActiveAgents.Any(agent => agent.IsHuman)))
        {
            sideHadAgents[(int)side] = true;
            return false;
        }

        return sideHadAgents[(int)side];
    }
}
