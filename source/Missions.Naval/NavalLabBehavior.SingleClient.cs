#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Missions.Battles;
using NavalDLC.GauntletUI.MissionViews;
using NavalDLC.Missions.AI.TeamAI;
using NavalDLC.Missions.AI.Tactics;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.ShipControl;
using NavalDLC.View.MissionViews;
using NavalDLC.View.MissionViews.Order;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace Missions.Naval;

internal sealed partial class NavalLabBehavior
{
    internal Func<bool> NativeAuthority;
    private Team[] nativeTeams;
    private bool nativeDeploymentAttempted;
    private bool nativeDeploymentComplete;
    private bool nativeTerminalHold;
    private int nativeDeploymentCallbacks;
    private int nativeAfterDeploymentCallbacks;
    private int nativeOrders;
    private string lastNativeOrder;

    internal bool CanUseNativeControls => HasNativeViews && nativeDeploymentComplete && !nativeTerminalHold
        && Blocker == null && NativeAuthority?.Invoke() == true && HasNativeOwnerIdentity();

    private bool HasNativeOwnerIdentity()
    {
        if (Ships.Length != manifest.Ships.Length || Agents.Length != manifest.Combatants.Length || LocalShip == null) return false;
        var main = LocalCaptain;
        var ship = LocalShip;
        var playerShip = Mission.GetMissionBehavior<NavalShipsLogic>()?.PlayerControlledShip;
        return main != null && main == Mission.MainAgent && main.IsPlayerControlled
            && ship.Captain == main && main.Formation == ship.Formation && ship.Formation.PlayerOwner == main
            && Mission.PlayerTeam == ship.Team && ship.Team.PlayerOrderController.Owner == main
            && (playerShip == null || playerShip == ship);
    }

    internal IEnumerable<MissionBehavior> CreateNativeViews(Mission mission)
    {
        var control = NavalViewCreator.CreateMissionShipControlView(mission);
        var orders = NavalViewCreator.CreateNavalOrderUIHandler(mission);
        if (control is not MissionGauntletShipControlView navalControl || orders is not MissionGauntletNavalOrderUIHandler)
            throw new InvalidOperationException("The active native naval Gauntlet overrides are required.");
        if (HotKeyManager.GetCategory("NavalShipControlsHotKeyCategory") == null)
            throw new InvalidOperationException("Native naval hotkeys are unavailable.");
        navalControl.SuspendFeature(MissionGauntletShipControlView.ShipControlFeatureFlags.AttemptBoarding
            | MissionGauntletShipControlView.ShipControlFeatureFlags.CutLoose
            | MissionGauntletShipControlView.ShipControlFeatureFlags.BallistaOrder
            | MissionGauntletShipControlView.ShipControlFeatureFlags.ShootBallista);
        if (IsTwoClientNative) navalControl.SuspendFeature(MissionGauntletShipControlView.ShipControlFeatureFlags.ToggleOarsmen
            | MissionGauntletShipControlView.ShipControlFeatureFlags.ShipFocus | MissionGauntletShipControlView.ShipControlFeatureFlags.ShipSelection);
        return new MissionBehavior[]
        {
            new NavalOrderTroopPlacer(null), new MissionFormationTargetSelectionHandler(),
            NavalViewCreator.CreateNavalShipTargetSelectionHandler(mission), new NavalMissionShipHighlightView(),
            orders, control, ViewCreator.CreateMissionAgentStatusUIHandler(mission),
            ViewCreator.CreateMissionMainAgentEquipmentController(mission),
            ViewCreator.CreateMissionMainAgentEquipDropView(mission),
            ViewCreator.CreateOptionsUIHandler(), ViewCreator.CreateMissionSingleplayerEscapeMenu(false)
        };
    }

    private void InitializeNativeTeams()
    {
        nativeTeams = new[] { Mission.Teams.Add(BattleSideEnum.Attacker), Mission.Teams.Add(BattleSideEnum.Defender) };
        Mission.PlayerTeam = nativeTeams[OwnSlot];
        Mission.PlayerTeam.SetPlayerRole(true, false);
        foreach (var team in nativeTeams)
        {
            team.AddTeamAI(new TeamAINavalComponent(Mission, team, 5f, 1f));
            // Native team decisions require the tactics normally supplied by NavalMissionCombatantsLogic.
            team.AddTacticOption(new TacticNavalBalancedOffense(team));
            if (team.Side == BattleSideEnum.Defender) team.AddTacticOption(new TacticNavalLineDefense(team));
        }
        Mission.GetMissionBehavior<NavalShipsLogic>().SetDeploymentMode(true);
        Mission.GetMissionBehavior<NavalAgentsLogic>().SetDeploymentMode(true);
        Mission.SetMissionMode(MissionMode.Deployment, true);
        Mission.AllowAiTicking = false;
    }

    private void PrepareNativeDeployment()
    {
        var ship = LocalShip;
        var main = LocalCaptain;
        var agents = Mission.GetMissionBehavior<NavalAgentsLogic>();
        foreach (var fixtureShip in Ships)
        {
            var captain = Agents[Array.IndexOf(Ships, fixtureShip) * NavalLabManifest.CrewPerShip];
            agents.AssignCaptainToShip(captain, fixtureShip);
            fixtureShip.Formation.PlayerOwner = captain;
            if (IsTwoClientNative) fixtureShip.Formation.SetControlledByAI(false);
        }
        ship.Formation.PlayerOwner = main;
        Mission.PlayerTeam.PlayerOrderController.Owner = main;
        agents.SetSpawnReinforcementsOnTick(false);
        if (!HasNativeOwnerIdentity()) throw new InvalidOperationException("Native captain and order ownership were not established.");
    }

    internal string CompleteNativeDeployment()
    {
        if (!HasNativeViews) return "rejected:wrong_mode";
        if (nativeTerminalHold || Blocker != null) return "rejected:fixture_blocked";
        if ((IsTwoClientNative ? !CanPrepareTwoClientDeployment : NativeAuthority?.Invoke() != true) || !HasNativeOwnerIdentity()) return "rejected:owner_not_ready";
        if (nativeDeploymentComplete) return "already_deployed";
        var controlView = Mission.GetMissionBehavior<MissionGauntletShipControlView>();
        var orderView = Mission.GetMissionBehavior<MissionGauntletNavalOrderUIHandler>();
        if (controlView?._dataSource == null || controlView._gauntletLayer == null || orderView?._dataSource == null)
            return "rejected:native_views_not_ready";
        if (nativeDeploymentAttempted) return "rejected:deployment_already_attempted";
        nativeDeploymentAttempted = true;
        try
        {
            var ships = Mission.GetMissionBehavior<NavalShipsLogic>();
            var agents = Mission.GetMissionBehavior<NavalAgentsLogic>();
            ships.SetTeleportShips(false);
            Mission.OnDeploymentFinished();
            // Native ship-wide callbacks must precede the spawn-use shortcut for every crew station.
            if (IsSingleClientNative)
            {
                agents.GetTeamAgents(Mission.PlayerTeam.TeamSide, out var teamAgents);
                teamAgents.AssignAndTeleportCrewToShipMachines(Ships[0]);
            }
            agents.SetDeploymentMode(false);
            ships.SetDeploymentMode(false);
            foreach (var agent in Agents.Where(agent => agent.IsAIControlled))
            {
                agent.SetAlarmState(Agent.AIStateFlag.Alarmed);
                agent.SetIsAIPaused(false);
                if ((agent.GetAgentFlags() & AgentFlag.CanWieldWeapon) != 0) agent.ResetEnemyCaches();
                agent.HumanAIComponent?.SyncBehaviorParamsIfNecessary();
            }
            Mission.InitialPlayerAgent.SetDetachableFromFormation(true);
            Mission.InitialPlayerAgent.Controller = AgentControllerType.Player;
            Mission.AllowAiTicking = true;
            Mission.SetFallAvoidSystemActive(false);
            // Stock FinishDeployment clears DisableDying; this disposable lab deliberately retains it.
            Mission.OnAfterDeploymentFinished();
            Mission.SetMissionMode(MissionMode.Battle, true);
            if (IsSingleClientNative) Ships[0].SetController(ShipControllerType.None, autoUpdateController: true);
            else foreach (var ship in Ships) ship.SetController(factoryHost || ship == LocalShip
                ? ShipControllerType.Player : ShipControllerType.None, autoUpdateController: false);
            if (Blocker != null || nativeDeploymentCallbacks != 1 || nativeAfterDeploymentCallbacks != 1
                || !Mission.IsDeploymentFinished || Ships.Any(ship => !ship.IsDeployed
                    || ship.GameEntity.HasDynamicRigidBodyAndActiveSimulation() != (IsSingleClientNative || factoryHost)))
                throw new InvalidOperationException("Native deployment or active factory body was not confirmed.");
            nativeDeploymentComplete = true;
            Simulating = IsSingleClientNative || factoryHost;
            return "deployed";
        }
        catch (Exception exception)
        {
            Reject("deployment.failed:" + exception);
            Hold();
            return "failed:deployment";
        }
    }

    public override void OnDeploymentFinished()
    {
        if (HasNativeViews) nativeDeploymentCallbacks++;
    }

    public override void OnAfterDeploymentFinished()
    {
        if (HasNativeViews) nativeAfterDeploymentCallbacks++;
    }

    internal void ObserveNativeOrder(OrderType order)
    {
        nativeOrders++;
        lastNativeOrder = order.ToString();
    }

    private void CancelNativeControls()
    {
        var main = LocalCaptain;
        var point = LocalShip?.ShipControllerMachine?.PilotStandingPoint;
        if (main != null && main.Pointer != UIntPtr.Zero && main.IsActive())
        {
            // An uncertain synthetic dispatch is never retried as terminal cleanup.
            if (!HasUncertainNativeHelmDispatch && point != null && point.GameEntity.IsValid
                && main.CurrentlyUsedGameObject == point && point.UserAgent == main)
                main.StopUsingGameObject();
            main.MovementInputVector = TaleWorlds.Library.Vec2.Zero;
        }
    }

    private object InspectNativeControls()
    {
        if (!HasNativeViews) return null;
        var ship = LocalShip;
        var controls = Mission?.GetMissionBehavior<MissionShipControlView>();
        var gauntlet = controls as MissionGauntletShipControlView;
        var orders = Mission?.GetMissionBehavior<MissionGauntletNavalOrderUIHandler>();
        return new
        {
            mode = manifest.Mode.ToString(), ownSlot = OwnSlot,
            unsupported = IsTwoClientNative ? "formation orders, oars allocation, station reassignment, boarding, capture, weapons" : null,
            deploymentAttempted = nativeDeploymentAttempted, deploymentComplete = nativeDeploymentComplete,
            terminalHold = nativeTerminalHold, nativeDeploymentCallbacks, nativeAfterDeploymentCallbacks,
            inputEnabled = CanUseNativeControls, nativeOrders, lastNativeOrder,
            missionMode = Mission?.Mode.ToString(), nativeDeploymentFinished = Mission?.IsDeploymentFinished,
            controlView = controls?.GetType().FullName, orderView = orders?.GetType().FullName,
            movieLoaded = gauntlet?._gauntletLayer != null, controlDataSource = gauntlet?._dataSource != null,
            categoryRegistered = gauntlet?.MissionScreen?.SceneLayer?.Input.IsCategoryRegistered(HotKeyManager.GetCategory("NavalShipControlsHotKeyCategory")),
            controllerMachineMatches = controls?.ControllerMachine != null && controls.ControllerMachine == ship?.ShipControllerMachine,
            playerControlledShipMatches = ship != null && Mission.GetMissionBehavior<NavalShipsLogic>()?.PlayerControlledShip == ship,
            shipController = ship?.Controller?.GetType().FullName,
            sentInputSequence = nativeInputSequence, lastHelmPermission,
            stationManifests = appliedStations.Values.ToArray(),
            storedInputs = IsTwoClientNative ? Ships.Select(item => item.PlayerController?._inputRecord).ToArray() : null,
            captain = InspectHelmAgent(ship?.Captain), pilot = InspectHelmAgent(ship?.ShipControllerMachine?.PilotAgent),
            mainAgent = InspectHelmAgent(Mission?.MainAgent),
            oarsmenLevel = ship?.ShipOrder?.OarsmenLevel,
            movementOrder = ship?.ShipOrder?.MovementOrderEnum.ToString(),
            crewStations = ship?.ShipOarMachines.Select(machine => InspectHelmAgent(machine.PilotAgent)).ToArray(),
            behaviors = Mission?.MissionBehaviors.Select(item => item.GetType().FullName).ToArray()
        };
    }
}
#endif
