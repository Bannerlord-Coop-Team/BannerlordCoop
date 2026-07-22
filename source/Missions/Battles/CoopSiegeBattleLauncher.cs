using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
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
/// Coop replacement for <c>SandBoxMissions.OpenSiegeMissionWithDeployment</c> (walls assault only),
/// with the same coop swaps as <see cref="CoopFieldBattleLauncher"/>. The mission opens under the
/// exact name "SiegeMissionWithDeployment" so the native siege view stack attaches. BattleEndLogic
/// deliberately does not arm the defender pull-back: the lords-hall stage is not supported, so the
/// walls mission only ends in the resolved victory states the result-commit flow already handles.
/// </summary>
internal class CoopSiegeBattleLauncher : ICoopSiegeBattleLauncher
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopSiegeBattleLauncher>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ICoopBattleBehaviorAttacher behaviorAttacher;

    public CoopSiegeBattleLauncher(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        ICoopBattleBehaviorAttacher behaviorAttacher)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.behaviorAttacher = behaviorAttacher;
    }

    public Mission OpenCoopSiegeBattle(MissionInitializerRecord rec, float[] wallHitPointRatios,
        List<MissionSiegeWeapon> attackerWeapons, List<MissionSiegeWeapon> defenderWeapons)
    {
        var mapEvent = PlayerEncounter.Battle ?? MobileParty.MainParty?.MapEvent;
        if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var mapEventId))
        {
            Logger.Error("[BattleSync] Cannot open coop siege battle: no resolvable map event");
            return null;
        }

        var mission = CreateCoopSiegeBattle(rec, mapEventId, wallHitPointRatios, attackerWeapons, defenderWeapons);

        // Same post-open coop entry as the field launcher: the controller requests the P2P instance and
        // the host handler requests this client's OWN troop reserves; the host election follows at
        // mission-ready (CoopBattleController.AfterStart, once loading finishes).
        messageBroker.Publish(mapEvent, new PlayerEnteredBattle(mapEvent));
        return mission;
    }

    private Mission CreateCoopSiegeBattle(MissionInitializerRecord rec, string mapEventId, float[] wallHitPointRatios,
        List<MissionSiegeWeapon> attackerWeapons, List<MissionSiegeWeapon> defenderWeapons)
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
            var defenderSupplier = new CoopTroopSupplier(mapEventId, BattleSideEnum.Defender, objectManager);
            var attackerSupplier = new CoopTroopSupplier(mapEventId, BattleSideEnum.Attacker, objectManager);
            CoopTroopSupplierRegistry.Register(defenderSupplier);
            CoopTroopSupplierRegistry.Register(attackerSupplier);

            // BattleSizeType.Siege selects the siege battle-size config in the spawn logic.
            var spawnLogic = new DefaultBattleMissionAgentSpawnLogic(
                new IMissionTroopSupplier[] { defenderSupplier, attackerSupplier },
                PartyBase.MainParty.Side,
                Mission.BattleSizeType.Siege);

            var mapEvent = MobileParty.MainParty.MapEvent;
            var behaviors = new List<MissionBehavior>
            {
                new BattleSpawnLogic("battle_set"),
                new MissionOptionsComponent(),
                new CampaignMissionComponent(),
                new BattleEndLogic(),
                new BattleReinforcementsSpawnController(),
                new MissionCombatantsLogic(
                    (IEnumerable<IBattleCombatant>)mapEvent.InvolvedParties,
                    PartyBase.MainParty,
                    mapEvent.GetLeaderParty(BattleSideEnum.Defender),
                    mapEvent.GetLeaderParty(BattleSideEnum.Attacker),
                    Mission.MissionTeamAITypeEnum.Siege,
                    isPlayerSergeant),
                new SiegeMissionPreparationHandler(false, false, wallHitPointRatios, hasAnySiegeTower),
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

            behaviors.Add(new CoopBattleMissionSpawnHandler(defenderSupplier, attackerSupplier));
            behaviors.Add(spawnLogic);
            behaviors.Add(new BattlePowerCalculationLogic());
            behaviors.Add(new BattleObserverMissionLogic());
            behaviors.Add(new BattleAgentLogic());
            behaviors.Add(new BattleSurgeonLogic());
            behaviors.Add(new MountAgentLogic());
            behaviors.Add(new BannerBearerLogic());
            behaviors.Add(new AgentHumanAILogic());
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
            behaviors.Add(new MissionSiegeEnginesLogic(defenderWeapons, attackerWeapons));
            behaviors.Add(new SiegeDeploymentHandler(isPlayerAttacker));
            behaviors.Add(new CoopSiegeDeploymentMissionController(isPlayerAttacker));

            return behaviors.ToArray();
        }, true, true);

        // OpenNew only constructs and pushes the MissionState. Vanilla invokes behavior initialization later,
        // after asynchronous scene loading; bind the deterministic wall-dressing seed to this mission until
        // SiegeDestructionSeedPatch consumes it inside ArrangeDestructedMeshes.
        SiegeSceneDestructionGate.Begin(mission, mapEventId);

        behaviorAttacher.Attach(mission);

        mission.SetPlayerCanTakeControlOfAnotherAgentWhenDead();
        Logger.Information("[BattleSync] Opened coop siege battle for {MapEventId} (player side {Side})",
            mapEventId, PartyBase.MainParty.Side);
        return mission;
    }
}
