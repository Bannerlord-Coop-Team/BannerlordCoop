using GameInterface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.UI;
using HarmonyLib;
using SandBox;
using SandBox.Missions.MissionLogics;
using SandBox.Missions.MissionLogics.Arena;
using SandBox.Tournaments.MissionLogics;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using Missions.Tournaments.Patches;
using Missions.Tournaments.Spectators;

namespace Missions.Tournaments;

public class CoopTournamentLauncher : ICoopTournamentLauncher
{
    public static IReadOnlyList<Type> BehaviorOrder { get; } = new[]
    {
        typeof(CampaignMissionComponent),
        typeof(EquipmentControllerLeaveLogic),
        typeof(CoopTournamentFightMissionController),
        typeof(CoopTournamentBehavior),
        typeof(AgentVictoryLogic),
        typeof(MissionAgentPanicHandler),
        typeof(AgentHumanAILogic),
        typeof(ArenaAgentStateDeciderLogic),
        typeof(MissionHardBorderPlacer),
        typeof(MissionBoundaryPlacer),
        typeof(TournamentSpectatorBarrierPlacer),
        typeof(MissionOptionsComponent),
        typeof(HighlightsController),
        typeof(SandboxHighlightsController),
        typeof(CoopTournamentController)
    };

    private readonly IObjectManager objectManager;
    private readonly ITournamentGameInterface tournamentGameInterface;
    private readonly Func<CoopTournamentController> controllerFactory;

    public CoopTournamentLauncher(
        Harmony harmony,
        IObjectManager objectManager,
        ITournamentGameInterface tournamentGameInterface,
        Func<CoopTournamentController> controllerFactory)
    {
        this.objectManager = objectManager;
        this.tournamentGameInterface = tournamentGameInterface;
        this.controllerFactory = controllerFactory;
        TournamentCombatPatchInstaller.Install(harmony);
    }

    public Mission OpenCoopTournament(TournamentSessionSnapshot snapshot, bool isSpectator)
    {
        snapshot = TournamentSessionSnapshotNormalizer.Normalize(snapshot);
        if (snapshot == null || string.IsNullOrEmpty(snapshot.SceneName)) return null;
        if (!objectManager.TryGetObject(snapshot.TownId, out Town town)) return null;

        TournamentGame nativeTournament = Campaign.Current?.TournamentManager?.GetTournamentGame(town);
        if (nativeTournament?.GetType() != typeof(FightTournamentGame)) return null;
        if (!tournamentGameInterface.TryRehydrateGame(snapshot, out FightTournamentGame tournamentGame)) return null;
        if (!ContainerProvider.TryResolve<TournamentMissionUIContext>(out var uiContext)) return null;

        uiContext.Set(snapshot);
        MissionInitializerRecord initializer = SandBoxMissions.CreateSandBoxMissionInitializerRecord(
            snapshot.SceneName,
            string.Empty,
            false,
            DecalAtlasGroup.Town);
        CoopTournamentController coopController = null;
        CoopTournamentBehavior tournamentBehavior = null;
        CoopTournamentFightMissionController fightController = null;

        Mission mission = MissionState.OpenNew(
            "TournamentFight",
            initializer,
            _ => CreateBehaviors(
                tournamentGame,
                town.Settlement,
                town.Culture,
                !isSpectator,
                snapshot,
                out coopController,
                out tournamentBehavior,
                out fightController),
            true,
            true);

        if (mission == null)
        {
            uiContext.Clear(snapshot.SessionId);
            return null;
        }

        coopController.Initialize(snapshot, tournamentBehavior, fightController, mission);
        return mission;
    }

    private IEnumerable<MissionBehavior> CreateBehaviors(
        TournamentGame tournamentGame,
        Settlement settlement,
        CultureObject culture,
        bool isPlayerParticipating,
        TournamentSessionSnapshot snapshot,
        out CoopTournamentController coopController,
        out CoopTournamentBehavior tournamentBehavior,
        out CoopTournamentFightMissionController fightController)
    {
        fightController = new CoopTournamentFightMissionController(culture);
        tournamentBehavior = new CoopTournamentBehavior(
            tournamentGame,
            settlement,
            fightController,
            isPlayerParticipating,
            snapshot,
            new TournamentNativeBracketHydrator(objectManager));
        coopController = controllerFactory();

        // v1.4.7 TournamentMissionStarter order, with only the fight controller/behavior replaced and the
        // coop controller appended. The mission name keeps the complete native TournamentFight view set;
        // CoopTournamentMissionView overrides only MissionTournamentView.
        MissionBehavior[] behaviors =
        {
            new CampaignMissionComponent(),
            new EquipmentControllerLeaveLogic(),
            fightController,
            tournamentBehavior,
            new AgentVictoryLogic(),
            new MissionAgentPanicHandler(),
            new AgentHumanAILogic(),
            new ArenaAgentStateDeciderLogic(),
            new MissionHardBorderPlacer(),
            new MissionBoundaryPlacer(),
            new TournamentSpectatorBarrierPlacer(),
            new MissionOptionsComponent(),
            new HighlightsController(),
            new SandboxHighlightsController(),
            coopController
        };
        if (!behaviors.Select(behavior => behavior.GetType()).SequenceEqual(BehaviorOrder))
            throw new InvalidOperationException("TournamentFight mission behavior composition changed");
        return behaviors;
    }
}
