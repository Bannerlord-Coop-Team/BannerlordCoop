using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.MapEvents.Extensions;
using SandBox.Missions.MissionLogics;
using SandBox.Missions.MissionLogics.Towns;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Missions.Handlers;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers.Logic;

namespace Missions.Battles;

/// <summary>
/// Coop replacement for <c>SandBoxMissions.OpenSiegeMissionWithDeployment</c> for walls assaults and
/// siege-engine ambushes, and for <c>SandBoxMissions.OpenSiegeLordsHallFightMission</c> for the keep after the
/// walls, with the same coop swaps as <see cref="CoopFieldBattleLauncher"/>. Each opens under the native mission
/// name so the native view stack attaches.
/// </summary>
internal class CoopSiegeBattleLauncher : ICoopSiegeBattleLauncher
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopSiegeBattleLauncher>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ICoopBattleBehaviorAttacher behaviorAttacher;
    private readonly IBattleAgentBudget agentBudget;

    public CoopSiegeBattleLauncher(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        ICoopBattleBehaviorAttacher behaviorAttacher,
        IBattleAgentBudget agentBudget)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.behaviorAttacher = behaviorAttacher;
        this.agentBudget = agentBudget;
    }

    public Mission OpenCoopSiegeBattle(MissionInitializerRecord rec, float[] wallHitPointRatios,
        List<MissionSiegeWeapon> attackerWeapons, List<MissionSiegeWeapon> defenderWeapons,
        bool isSallyOut)
    {
        var mapEvent = PlayerEncounter.Battle ?? MobileParty.MainParty?.MapEvent;
        if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var mapEventId))
        {
            Logger.Error("[BattleSync] Cannot open coop siege battle: no resolvable map event");
            return null;
        }

        var mission = CreateCoopSiegeBattle(rec, mapEventId,
            wallHitPointRatios, attackerWeapons, defenderWeapons, isSallyOut);
        if (mission == null) return null;

        // Same post-open coop entry as the field launcher: the controller requests the P2P instance and
        // the host handler requests this client's OWN troop reserves; the host election follows at
        // mission-ready (CoopBattleController.AfterStart, once loading finishes).
        messageBroker.Publish(mapEvent, new PlayerEnteredBattle(mapEvent));
        return mission;
    }

    private Mission CreateCoopSiegeBattle(MissionInitializerRecord rec, string mapEventId,
        float[] wallHitPointRatios,
        List<MissionSiegeWeapon> attackerWeapons, List<MissionSiegeWeapon> defenderWeapons,
        bool isSallyOut)
    {
        bool hasAnySiegeTower = attackerWeapons.Exists(weapon => weapon.Type == DefaultSiegeEngineTypes.SiegeTower);
        bool isPlayerSergeant = MobileParty.MainParty.MapEvent.IsPlayerSergeant();
        bool isPlayerInArmy = MobileParty.MainParty.Army != null;
        bool isPlayerAttacker = PartyBase.MainParty.Side == BattleSideEnum.Attacker;
        List<string> heroesOnPlayerSideByPriority = CoopFieldBattleLauncher.OwnPartyHeroesByPriority();

        Hero attackerLeader = MapEvent.PlayerMapEvent.AttackerSide.LeaderParty.LeaderHero;
        TextObject attackerGeneralName = attackerLeader?.Name;
        Hero defenderLeader = MapEvent.PlayerMapEvent.DefenderSide.LeaderParty.LeaderHero;
        TextObject defenderGeneralName = defenderLeader?.Name;

        var mission = MissionState.OpenNew("SiegeMissionWithDeployment", rec, (InitializeMissionBehaviorsDelegate)delegate
        {
            var defenderSupplier = new CoopTroopSupplier(mapEventId, BattleSideEnum.Defender, objectManager,
                agentBudget);
            var attackerSupplier = new CoopTroopSupplier(mapEventId, BattleSideEnum.Attacker, objectManager,
                agentBudget);
            CoopTroopSupplierRegistry.Register(defenderSupplier);
            CoopTroopSupplierRegistry.Register(attackerSupplier);

            var battleSizeType = isSallyOut
                ? Mission.BattleSizeType.SallyOut
                : Mission.BattleSizeType.Siege;
            var spawnLogic = new DefaultBattleMissionAgentSpawnLogic(
                new IMissionTroopSupplier[] { defenderSupplier, attackerSupplier },
                PartyBase.MainParty.Side,
                battleSizeType);

            var mapEvent = MobileParty.MainParty.MapEvent;
            var behaviors = new List<MissionBehavior>
            {
                new BattleSpawnLogic(isSallyOut ? "sally_out_set" : "battle_set"),
                new MissionOptionsComponent(),
                new CampaignMissionComponent(),
                CreateWallsEndLogic(mapEvent, isSallyOut),
                new BattleReinforcementsSpawnController(),
                new MissionCombatantsLogic(
                    (IEnumerable<IBattleCombatant>)mapEvent.InvolvedParties,
                    PartyBase.MainParty,
                    mapEvent.GetLeaderParty(BattleSideEnum.Defender),
                    mapEvent.GetLeaderParty(BattleSideEnum.Attacker),
                    isSallyOut ? Mission.MissionTeamAITypeEnum.SallyOut : Mission.MissionTeamAITypeEnum.Siege,
                    isPlayerSergeant),
                new CoopSiegeMissionPreparationHandler(isSallyOut, wallHitPointRatios, hasAnySiegeTower, mapEventId),
                new CampaignSiegeStateHandler(),
            };

            // Same town resolution as the private SandBoxMissions.GetCurrentTown.
            var currentTown = Settlement.CurrentSettlement?.IsTown == true
                ? Settlement.CurrentSettlement
                : (MapEvent.PlayerMapEvent?.MapEventSettlement?.IsTown == true ? MapEvent.PlayerMapEvent.MapEventSettlement : null);
            if (currentTown != null)
            {
                behaviors.Add(new WorkshopMissionHandler(currentTown));
            }

            if (isSallyOut)
                behaviors.Add(new SandBoxSallyOutMissionController(isSallyOutAmbush: true));

            behaviors.Add(new CoopBattleMissionSpawnHandler(defenderSupplier, attackerSupplier, messageBroker,
                PartyBase.MainParty.Side, isSallyOut: isSallyOut));
            behaviors.Add(spawnLogic);
            behaviors.Add(new BattlePowerCalculationLogic());
            behaviors.Add(new BattleObserverMissionLogic());
            behaviors.Add(new BattleAgentLogic());
            behaviors.Add(new BattleSurgeonLogic());
            behaviors.Add(new MountAgentLogic());
            behaviors.Add(new BannerBearerLogic());
            behaviors.Add(new AgentHumanAILogic());
            if (!isSallyOut)
                behaviors.Add(new AmmoSupplyLogic(new List<BattleSideEnum> { BattleSideEnum.Defender }));
            behaviors.Add(new AgentVictoryLogic());
            behaviors.Add(new AssignPlayerRoleInTeamMissionController(!isPlayerSergeant, isPlayerSergeant, isPlayerInArmy, heroesOnPlayerSideByPriority));
            behaviors.Add(new SandboxGeneralsAndCaptainsAssignmentLogic(attackerGeneralName, defenderGeneralName, null, null, createBodyguard: false));
            behaviors.Add(new MissionAgentPanicHandler());
            behaviors.Add(new MissionBoundaryPlacer());
            behaviors.Add(new MissionBoundaryCrossingHandler(10f));
            behaviors.Add(new AgentMoraleInteractionLogic());
            behaviors.Add(new HighlightsController());
            behaviors.Add(new BattleHighlightsController());
            behaviors.Add(new EquipmentControllerLeaveLogic());
            behaviors.Add(isSallyOut
                ? new MissionSiegeEnginesLogic(new List<MissionSiegeWeapon>(), attackerWeapons)
                : new MissionSiegeEnginesLogic(defenderWeapons, attackerWeapons));
            behaviors.Add(new SiegeDeploymentHandler(isPlayerAttacker));
            behaviors.Add(new CoopSiegeDeploymentMissionController(isPlayerAttacker));

            return behaviors.ToArray();
        }, true, true);

        behaviorAttacher.Attach(mission);

        mission.SetPlayerCanTakeControlOfAnotherAgentWhenDead();
        Logger.Information("[BattleSync] Opened coop siege battle for {MapEventId} (player side {Side}, sallyOut={SallyOut})",
            mapEventId, PartyBase.MainParty.Side, isSallyOut);
        return mission;
    }

    // Vanilla arms the defenders' fall back to the keep for an attacking player. The server opens the keep only for
    // attacking players with no player defending, so the walls keep their plain end otherwise.
    private static BattleEndLogic CreateWallsEndLogic(MapEvent mapEvent, bool isSallyOut)
    {
        if (isSallyOut || PartyBase.MainParty.Side != BattleSideEnum.Attacker || !mapEvent.CanFallBackToKeep())
            return new BattleEndLogic();

        Logger.Information("[BattleSync] Armed the defender pull-back to the keep for {Settlement}",
            mapEvent.MapEventSettlement.StringId);
        return new CoopSiegeWallsEndLogic(
            Campaign.Current.Models.SiegeLordsHallFightModel.DefenderTroopNumberForSuccessfulPullBack);
    }

    public Mission OpenCoopLordsHallBattle(MissionInitializerRecord rec)
    {
        var mapEvent = PlayerEncounter.Battle ?? MobileParty.MainParty?.MapEvent;
        if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var mapEventId))
        {
            Logger.Error("[BattleSync] Cannot open coop keep battle: no resolvable map event");
            return null;
        }

        var mission = MissionState.OpenNew("SiegeLordsHallFightMission", rec, (InitializeMissionBehaviorsDelegate)delegate
        {
            var defenderSupplier = new CoopTroopSupplier(mapEventId, BattleSideEnum.Defender, objectManager,
                agentBudget);
            var attackerSupplier = new CoopTroopSupplier(mapEventId, BattleSideEnum.Attacker, objectManager,
                agentBudget);
            CoopTroopSupplierRegistry.Register(defenderSupplier);
            CoopTroopSupplierRegistry.Register(attackerSupplier);

            return new MissionBehavior[]
            {
                new MissionOptionsComponent(),
                new CampaignMissionComponent(),
                new BattleEndLogic(),
                new MissionCombatantsLogic(
                    (IEnumerable<IBattleCombatant>)mapEvent.InvolvedParties,
                    PartyBase.MainParty,
                    mapEvent.GetLeaderParty(BattleSideEnum.Defender),
                    mapEvent.GetLeaderParty(BattleSideEnum.Attacker),
                    Mission.MissionTeamAITypeEnum.NoTeamAI,
                    isPlayerSergeant: false),
                new AgentHumanAILogic(),
                new CoopLordsHallFightMissionController(defenderSupplier, attackerSupplier, PartyBase.MainParty.Side,
                    Campaign.Current.Models.SiegeLordsHallFightModel),
                new BattleObserverMissionLogic(),
                new BattleAgentLogic(),
                new AgentVictoryLogic(),
                new AmmoSupplyLogic(new List<BattleSideEnum> { BattleSideEnum.Defender }),
                new MissionHardBorderPlacer(),
                new MissionBoundaryPlacer(),
                new MissionBoundaryCrossingHandler(),
                new EquipmentControllerLeaveLogic(),
                new BattleMissionAgentInteractionLogic(),
                new HighlightsController(),
                new BattleHighlightsController(),
                new BattleSurgeonLogic(),
            };
        }, true, true);

        behaviorAttacher.Attach(mission);
        messageBroker.Publish(mapEvent, new PlayerEnteredBattle(mapEvent));
        Logger.Information("[BattleSync] Opened coop keep battle for {MapEventId} (player side {Side})",
            mapEventId, PartyBase.MainParty.Side);
        return mission;
    }
}
