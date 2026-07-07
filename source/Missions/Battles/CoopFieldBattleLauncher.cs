using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using Helpers;
using SandBox.Missions.MissionLogics;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Missions.Handlers;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers.Logic;

namespace Missions.Battles;

/// <summary>
/// Coop replacement for <c>SandBoxMissions.OpenBattleMission</c>. Mirrors the native field-battle behavior
/// list but: (1) builds the spawn logic with our <see cref="CoopTroopSupplier"/>s so each client fields only
/// the troops it OWNS (its own party; plus the AI/enemy side for the host) — see
/// <see cref="CoopBattleMissionSpawnHandler"/> for the per-side sizing; (2) keeps the native deployment
/// behaviors so each client runs its own Order-of-Battle phase (Phase A); and (3) attaches the coop P2P behaviors and raises
/// <see cref="PlayerEnteredBattle"/> itself (the native path did this from <c>BattleMissionEntryPatch</c>,
/// which does not fire for our <c>MissionState.OpenNew</c> mission).
/// <para>
/// The suppliers are registered with <see cref="CoopTroopSupplierRegistry"/> at build, so the server reserve
/// (requested via <see cref="PlayerEnteredBattle"/>) feeds them during scene load — before <c>AfterStart</c>.
/// </para>
/// </summary>
internal class CoopFieldBattleLauncher : ICoopFieldBattleLauncher
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopFieldBattleLauncher>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ICoopBattleBehaviorAttacher behaviorAttacher;

    public CoopFieldBattleLauncher(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        ICoopBattleBehaviorAttacher behaviorAttacher)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.behaviorAttacher = behaviorAttacher;
    }

    public Mission OpenCoopFieldBattle(MissionInitializerRecord rec)
    {
        var mapEvent = PlayerEncounter.Battle ?? MobileParty.MainParty?.MapEvent;
        if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var mapEventId))
        {
            Logger.Error("[BattleSync] Cannot open coop field battle: no resolvable map event");
            return null;
        }

        var mission = CreateCoopFieldBattle(rec, mapEventId);

        // Same post-open coop entry the native path drove via BattleMissionEntryPatch: the controller requests
        // the P2P instance and the host handler requests election + this client's troop reserves. The reserves
        // reach the (already-registered) suppliers during scene load, before AfterStart sizes them.
        messageBroker.Publish(mapEvent, new PlayerEnteredBattle(mapEvent));
        return mission;
    }

    private Mission CreateCoopFieldBattle(MissionInitializerRecord rec, string mapEventId)
    {
        bool isPlayerSergeant = MobileParty.MainParty.MapEvent.IsPlayerSergeant();
        bool isPlayerInArmy = MobileParty.MainParty.Army != null;
        // Same source the native OpenBattleMission uses to orient deployment (which side's spawn frames the
        // player deploys onto). PartyBase.MainParty.Side is the authoritative battle side — already used below
        // to drive the spawn logic's player side.
        bool isPlayerAttacker = PartyBase.MainParty.Side == BattleSideEnum.Attacker;
        // In a coop battle each client deploys only its OWN party (the rest of the side arrives as host-owned
        // puppets), so the deployment role/captain list must be scoped to the local player's party. The native
        // HeroHelper.OrderHeroesOnPlayerSideByPriority returns the leader hero of EVERY party on the side, which
        // would seat the host's and the AI lords' heroes in a non-host's Order of Battle.
        List<string> heroesOnPlayerSideByPriority = OwnPartyHeroesByPriority();

        Hero attackerLeader = MapEvent.PlayerMapEvent.AttackerSide.LeaderParty.LeaderHero;
        TextObject attackerGeneralName = attackerLeader?.Name;
        Hero defenderLeader = MapEvent.PlayerMapEvent.DefenderSide.LeaderParty.LeaderHero;
        TextObject defenderGeneralName = defenderLeader?.Name;

        var mission = MissionState.OpenNew("Battle", rec, (InitializeMissionBehaviorsDelegate)delegate
        {
            // Each client fields only what it OWNS. Registered so the server reserve (requested below via
            // PlayerEnteredBattle) feeds these during scene load; the spawn handler then sizes each side.
            var defenderSupplier = new CoopTroopSupplier(mapEventId, BattleSideEnum.Defender, objectManager);
            var attackerSupplier = new CoopTroopSupplier(mapEventId, BattleSideEnum.Attacker, objectManager);
            CoopTroopSupplierRegistry.Register(defenderSupplier);
            CoopTroopSupplierRegistry.Register(attackerSupplier);

            // suppliers[i] is the supplier for side i (Defender=0, Attacker=1); player side drives team/agent.
            var spawnLogic = new DefaultBattleMissionAgentSpawnLogic(
                new IMissionTroopSupplier[] { defenderSupplier, attackerSupplier },
                PartyBase.MainParty.Side,
                Mission.BattleSizeType.Battle);

            var mapEvent = MobileParty.MainParty.MapEvent;
            var behaviors = new List<MissionBehavior>
            {
                spawnLogic,
                new BattlePowerCalculationLogic(),
                new BattleSpawnLogic("battle_set"),
                new CoopBattleMissionSpawnHandler(defenderSupplier, attackerSupplier),
                new CampaignMissionComponent(),
                new BattleAgentLogic(),
                new MountAgentLogic(),
                new BannerBearerLogic(),
                new MissionOptionsComponent(),
                new BattleEndLogic(),
                new BattleReinforcementsSpawnController(),
                new MissionCombatantsLogic(
                    (IEnumerable<IBattleCombatant>)mapEvent.InvolvedParties,
                    PartyBase.MainParty,
                    mapEvent.GetLeaderParty(BattleSideEnum.Defender),
                    mapEvent.GetLeaderParty(BattleSideEnum.Attacker),
                    Mission.MissionTeamAITypeEnum.FieldBattle,
                    isPlayerSergeant),
                new BattleObserverMissionLogic(),
                new AgentHumanAILogic(),
                new AgentVictoryLogic(),
                new BattleSurgeonLogic(),
                new MissionAgentPanicHandler(),
                new BattleMissionAgentInteractionLogic(),
                new AgentMoraleInteractionLogic(),
                new AssignPlayerRoleInTeamMissionController(!isPlayerSergeant, isPlayerSergeant, isPlayerInArmy, heroesOnPlayerSideByPriority),
                new SandboxGeneralsAndCaptainsAssignmentLogic(attackerGeneralName, defenderGeneralName),
                new EquipmentControllerLeaveLogic(),
                new MissionHardBorderPlacer(),
                new MissionBoundaryPlacer(),
                new MissionBoundaryCrossingHandler(10f),
                new HighlightsController(),
                new BattleHighlightsController(),
                // Phase A deployment: the same two behaviors native OpenBattleMission adds last. They drive the
                // deployment views the "Battle" ViewMethod already attaches (DeploymentMissionView + Order of
                // Battle), spawn both sides frozen during SetupTeams, hold Mission.AllowAiTicking off, and start
                // the spawners + un-pause AI only on Start Battle (FinishDeployment). This replaces the coop
                // force-spawn shortcut (CoopBattleController.EnsureSidesSpawning), now removed.
                new CoopBattleDeploymentMissionController(isPlayerAttacker),
                new BattleDeploymentHandler(isPlayerAttacker),
            };

            return behaviors.ToArray();
        }, true, true);

        // Attach the coop P2P behaviors (a fresh CoopBattleController) BEFORE PlayerEnteredBattle is published
        // by our caller, so the controller is alive and subscribed for the instance request — the same
        // post-open attach the native path uses (BattleMissionEntryPatch).
        behaviorAttacher.Attach(mission);

        mission.SetPlayerCanTakeControlOfAnotherAgentWhenDead();
        Logger.Information("[BattleSync] Opened coop field battle for {MapEventId} (player side {Side})",
            mapEventId, PartyBase.MainParty.Side);
        return mission;
    }

    // The local player's own deployable heroes (its party leader + any companion heroes in the party), highest
    // sergeant-score first — the coop-scoped replacement for HeroHelper.OrderHeroesOnPlayerSideByPriority, which
    // spans the whole side. Carried as CharacterObject string ids, matching the native list that
    // AssignPlayerRoleInTeamMissionController consumes. Shared with the siege launcher.
    internal static List<string> OwnPartyHeroesByPriority()
    {
        var heroes = new List<Hero>();
        foreach (var member in MobileParty.MainParty.MemberRoster.GetTroopRoster())
            if (member.Character?.HeroObject is Hero hero)
                heroes.Add(hero);

        return heroes
            .OrderByDescending(h => Campaign.Current.Models.EncounterModel.GetCharacterSergeantScore(h))
            .Select(h => h.CharacterObject.StringId)
            .ToList();
    }
}
