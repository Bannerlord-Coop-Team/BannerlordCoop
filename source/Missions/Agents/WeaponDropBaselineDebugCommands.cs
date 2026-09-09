#if DEBUG
using Common;
using Common.Commands;
using GameInterface;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Missions.Agents.Messages;
using Missions.Battles;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents;

internal static class WeaponDropBaselineDebugCommands
{
    private sealed class WeaponState
    {
        public bool IsEmpty { get; set; }
        public string ItemObjectId { get; set; }
        public string ItemStringId { get; set; }
        public string ModifierObjectId { get; set; }
        public string ModifierStringId { get; set; }
        public int RawDataForNetwork { get; set; }
        public int Amount { get; set; }
        public string BannerCode { get; set; }
    }

    private sealed class WorldItemState
    {
        public string WorldItemId { get; set; }
        public int MissionObjectId { get; set; }
        public bool CreatedAtRuntime { get; set; }
        public bool IsRemoved { get; set; }
        public bool IsDeactivated { get; set; }
        public bool GameEntityValid { get; set; }
        public WeaponState Weapon { get; set; }
    }

    private sealed class WeaponSlotState
    {
        public int EquipmentSlot { get; set; }
        public string EquipmentSlotName { get; set; }
        public WeaponState Weapon { get; set; }
    }

    private sealed class LocalAgentState
    {
        public bool Success { get; set; }
        public string LocalRole { get; set; }
        public string LocalControllerId { get; set; }
        public string BattleInstanceId { get; set; }
        public string AgentId { get; set; }
        public int AgentIndex { get; set; }
        public bool LocallyControlled { get; set; }
        public string CurrentAuthority { get; set; }
        public string OriginalOwner { get; set; }
        public WeaponSlotState[] WeaponSlots { get; set; }
    }

    private sealed class SnapshotState
    {
        public bool Success { get; set; }
        public string LocalRole { get; set; }
        public string LocalControllerId { get; set; }
        public string BattleInstanceId { get; set; }
        public string AgentId { get; set; }
        public int AgentIndex { get; set; }
        public bool IsMainAgent { get; set; }
        public bool LocallyControlled { get; set; }
        public string CurrentAuthority { get; set; }
        public string OriginalOwner { get; set; }
        public string MovementScopeId { get; set; }
        public int MovementId { get; set; }
        public long AuthorityRevision { get; set; }
        public string TeamSide { get; set; }
        public int EquipmentSlot { get; set; }
        public string EquipmentSlotName { get; set; }
        public WeaponState Slot { get; set; }
        public string[] RegisteredControllerIds { get; set; }
        public int RegisteredWorldItemCount { get; set; }
        public WorldItemState[] WorldItems { get; set; }
        public bool NativeDropInvoked { get; set; }
    }

    private sealed class ControllerRegistryState
    {
        public string ControllerId { get; set; }
        public int AgentCount { get; set; }
    }

    private sealed class RegistryDiagnosticState
    {
        public bool Success { get; set; }
        public string LocalRole { get; set; }
        public bool MissionPresent { get; set; }
        public bool CoopBattlePresent { get; set; }
        public string BattleInstanceId { get; set; }
        public string LocalControllerId { get; set; }
        public bool DeploymentActivated { get; set; }
        public bool DeploymentCommitted { get; set; }
        public bool DeploymentControllerPresent { get; set; }
        public bool DeploymentReady { get; set; }
        public bool MainAgentPresent { get; set; }
        public bool MainAgentRegistered { get; set; }
        public string QueriedAgentId { get; set; }
        public bool QueryIdValid { get; set; }
        public bool AgentRegistryAvailable { get; set; }
        public bool AgentRegistered { get; set; }
        public bool AgentActive { get; set; }
        public bool AgentInCurrentMission { get; set; }
        public bool AgentIsMainAgent { get; set; }
        public bool AgentLocallyControlled { get; set; }
        public int AgentIndex { get; set; }
        public string CurrentAuthority { get; set; }
        public string OriginalOwner { get; set; }
        public string MovementScopeId { get; set; }
        public int MovementId { get; set; }
        public long AuthorityRevision { get; set; }
        public string[] RegisteredControllerIds { get; set; }
        public ControllerRegistryState[] ControllerRegistry { get; set; }
    }

    private sealed class ReturningHeroObservationState
    {
        public bool Success { get; set; }
        public bool ObservationAvailable { get; set; }
        public string[] UnavailableReasons { get; set; } = Array.Empty<string>();
        public bool MissionPresent { get; set; }
        public bool CoopBattlePresent { get; set; }
        public bool CampaignServicesAvailable { get; set; }
        public string LocalControllerId { get; set; }
        public string BattleInstanceId { get; set; }
        public string RequestedControllerId { get; set; }
        public bool DeploymentActivated { get; set; }
        public bool DeploymentCommitted { get; set; }
        public bool DeploymentControllerPresent { get; set; }
        public bool DeploymentTeamSetupOver { get; set; }
        public bool PlayerRegistered { get; set; }
        public bool PlayerConnected { get; set; }
        public string RegisteredHeroId { get; set; }
        public string RegisteredPartyId { get; set; }
        public bool HeroResolved { get; set; }
        public bool HeroControlled { get; set; }
        public string HeroStringId { get; set; }
        public bool PartyResolved { get; set; }
        public bool PartyControlled { get; set; }
        public string PartyStringId { get; set; }
        public bool PartyActive { get; set; }
        public string PartyMapEventId { get; set; }
        public string PartyMapEventStringId { get; set; }
        public bool AgentRegistryAvailable { get; set; }
        public bool HeroAgentRegistered { get; set; }
        public string AgentId { get; set; }
        public bool AgentActive { get; set; }
        public bool AgentInCurrentMission { get; set; }
        public bool AgentIsMainAgent { get; set; }
        public int AgentIndex { get; set; }
        public float AgentHealth { get; set; }
        public string CurrentAuthority { get; set; }
        public string OriginalOwner { get; set; }
        public string MovementScopeId { get; set; }
        public int MovementId { get; set; }
        public long AuthorityRevision { get; set; }
        public string OriginPartyId { get; set; }
        public int? OriginSeed { get; set; }
        public CoopTroopSupplierDebugState SupplierState { get; set; }
        public PendingPuppetDebugState PendingPuppetState { get; set; }
        public string PendingPuppetStateError { get; set; }
    }

    private sealed class CapturedDropState
    {
        public string BattleInstanceId { get; set; }
        public Guid AgentId { get; set; }
        public EquipmentIndex EquipmentIndex { get; set; }
        public string OriginControllerId { get; set; }
        public NetworkWeaponDropped Message { get; set; }
        public bool CapturedAtOrigin { get; set; }
        public int CapturedOutgoingCount { get; set; }
        public int ReplaySendCount { get; set; }
    }

    private sealed class DuplicateDropState
    {
        public Guid DropId { get; set; }
        public Guid WorldItemId { get; set; }
        public Guid AgentId { get; set; }
        public EquipmentIndex EquipmentIndex { get; set; }
        public int Count { get; set; }
    }

    private sealed class ReplayStatusState
    {
        public bool Success { get; set; }
        public string BattleInstanceId { get; set; }
        public bool CaptureArmed { get; set; }
        public string CapturedBattleInstanceId { get; set; }
        public bool CapturedCurrentBattle { get; set; }
        public bool CapturedValidNetworkMessage { get; set; }
        public bool CapturedAtOrigin { get; set; }
        public string CapturedOriginControllerId { get; set; }
        public string CapturedDropId { get; set; }
        public string CapturedWorldItemId { get; set; }
        public string CapturedAgentId { get; set; }
        public int CapturedEquipmentSlot { get; set; }
        public int CapturedOutgoingCount { get; set; }
        public int ReplaySendCount { get; set; }
        public string ExpectedDropId { get; set; }
        public string ExpectedWorldItemId { get; set; }
        public bool DuplicateAlreadyAppliedObserved { get; set; }
        public int DuplicateAlreadyAppliedCount { get; set; }
        public string DuplicateAgentId { get; set; }
        public int DuplicateEquipmentSlot { get; set; }
    }

    private static readonly object CaptureGate = new object();
    private static readonly Dictionary<Guid, DuplicateDropState> DuplicateDrops =
        new Dictionary<Guid, DuplicateDropState>();
    private static CapturedDropState capturedDrop;

    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    public sealed class SnapshotCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.weapon_drop";

        public string Name => "snapshot";

        public string Description => "Reports one registered agent slot and every registered world item.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("agent_id", "The registered agent id."),
            new ExpectedArgs("equipment_slot", "The weapon equipment slot."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetActiveBattle(out Mission mission, out CoopBattleController controller, out string error))
                return Failed(error);
            if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var agentRegistry) ||
                !ContainerProvider.TryResolve<INetworkWorldItemRegistry>(out var worldItemRegistry) ||
                !ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            {
                return Failed("WEAPON_DROP_SNAPSHOT required mission services are unavailable");
            }
            if (!Guid.TryParse(args[0], out Guid agentId) ||
                !TryParseEquipmentIndex(args[1], out EquipmentIndex equipmentIndex, out error))
            {
                return Failed(error ?? "WEAPON_DROP_SNAPSHOT agent id is invalid");
            }
            if (!TryCaptureSnapshot(
                    mission,
                    controller,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    agentId,
                    equipmentIndex,
                    out SnapshotState snapshot,
                    out error))
            {
                return Failed(error);
            }

            return Succeeded("LIVE_TEST_JSON=" + JsonConvert.SerializeObject(snapshot));
        }
    }

    public sealed class LocalCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.weapon_drop";

        public string Name => "local";

        public string Description => "Reports the locally controlled main agent and weapon slots.";

        public CoopCommandSide Side => CoopCommandSide.Client;

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetActiveBattle(out Mission mission, out CoopBattleController controller, out string error))
                return Failed(error);
            if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var agentRegistry) ||
                !ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            {
                return Failed("WEAPON_DROP_LOCAL required mission services are unavailable");
            }

            Agent agent = Agent.Main;
            if (agent == null || !agent.IsActive() || agent.Mission != mission || agent.Equipment == null)
                return Failed("WEAPON_DROP_LOCAL no active main agent");
            if (!agentRegistry.TryGetAgentInfo(agent, out CoopAgentInfo agentInfo) ||
                !agentRegistry.IsLocallyControlled(agentInfo.AgentId))
            {
                return Failed("WEAPON_DROP_LOCAL main agent is not locally controlled");
            }

            var state = new LocalAgentState
            {
                Success = true,
                LocalRole = ModInformation.IsServer ? "server" : "client",
                LocalControllerId = controller.Session.OwnControllerId,
                BattleInstanceId = Convert.ToString(controller.Session.InstanceId, CultureInfo.InvariantCulture),
                AgentId = agentInfo.AgentId.ToString("D"),
                AgentIndex = agent.Index,
                LocallyControlled = true,
                CurrentAuthority = agentInfo.CurrentAuthority,
                OriginalOwner = agentInfo.OriginalOwner,
                WeaponSlots = Enumerable.Range(
                        (int)EquipmentIndex.WeaponItemBeginSlot,
                        (int)EquipmentIndex.NumAllWeaponSlots - (int)EquipmentIndex.WeaponItemBeginSlot)
                    .Select(index =>
                    {
                        EquipmentIndex equipmentIndex = (EquipmentIndex)index;
                        return new WeaponSlotState
                        {
                            EquipmentSlot = index,
                            EquipmentSlotName = equipmentIndex.ToString(),
                            Weapon = CaptureWeapon(agent.Equipment[equipmentIndex], objectManager),
                        };
                    })
                    .ToArray(),
            };
            return Succeeded("LIVE_TEST_JSON=" + JsonConvert.SerializeObject(state));
        }
    }

    public sealed class DiagnosticCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.weapon_drop";

        public string Name => "diagnostic";

        public string Description => "Reports mission, deployment, and registry state for one agent id.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("agent_id", "The agent id to inspect without requiring registration."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            Mission mission = Mission.Current;
            CoopBattleController controller = mission?.GetMissionBehavior<CoopBattleController>();
            var state = new RegistryDiagnosticState
            {
                Success = true,
                LocalRole = ModInformation.IsServer ? "server" : "client",
                MissionPresent = mission != null,
                CoopBattlePresent = controller != null,
                BattleInstanceId = controller == null
                    ? null
                    : Convert.ToString(controller.Session.InstanceId, CultureInfo.InvariantCulture),
                LocalControllerId = controller?.Session.OwnControllerId,
                DeploymentActivated = controller?.Deployment.IsActivated == true,
                DeploymentCommitted = controller?.Deployment.IsCommitted == true,
                DeploymentControllerPresent = mission?.GetMissionBehavior<DeploymentMissionController>() != null,
                DeploymentReady = mission?.GetMissionBehavior<DeploymentMissionController>()?.TeamSetupOver == true,
                MainAgentPresent = Agent.Main != null,
                AgentIndex = -1,
                QueriedAgentId = args[0],
                RegisteredControllerIds = Array.Empty<string>(),
                ControllerRegistry = Array.Empty<ControllerRegistryState>(),
            };

            if (!Guid.TryParse(args[0], out Guid agentId) || agentId == Guid.Empty)
                return Succeeded("LIVE_TEST_JSON=" + JsonConvert.SerializeObject(state));

            state.QueryIdValid = true;
            if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var agentRegistry))
                return Succeeded("LIVE_TEST_JSON=" + JsonConvert.SerializeObject(state));

            state.AgentRegistryAvailable = true;
            string[] controllerIds = agentRegistry.GetControllerIds()
                .OrderBy(controllerId => controllerId, StringComparer.Ordinal)
                .ToArray();
            state.RegisteredControllerIds = controllerIds;
            state.ControllerRegistry = controllerIds
                .Select(controllerId => new ControllerRegistryState
                {
                    ControllerId = controllerId,
                    AgentCount = agentRegistry.GetAgents(controllerId).Count,
                })
                .ToArray();

            if (Agent.Main != null && agentRegistry.TryGetAgentInfo(Agent.Main, out _))
                state.MainAgentRegistered = true;
            if (!agentRegistry.TryGetAgentInfo(agentId, out CoopAgentInfo agentInfo))
                return Succeeded("LIVE_TEST_JSON=" + JsonConvert.SerializeObject(state));

            Agent agent = agentInfo.Agent;
            state.AgentRegistered = true;
            state.AgentActive = agent != null && agent.IsActive();
            state.AgentInCurrentMission = agent != null && agent.Mission == mission;
            state.AgentIsMainAgent = ReferenceEquals(agent, Agent.Main);
            state.AgentLocallyControlled = agentRegistry.IsLocallyControlled(agentId);
            state.AgentIndex = agent?.Index ?? -1;
            state.CurrentAuthority = agentInfo.CurrentAuthority;
            state.OriginalOwner = agentInfo.OriginalOwner;
            state.MovementScopeId = agentInfo.MovementScopeId;
            state.MovementId = agentInfo.MovementId;
            state.AuthorityRevision = agentInfo.AuthorityRevision;
            return Succeeded("LIVE_TEST_JSON=" + JsonConvert.SerializeObject(state));
        }
    }

    public sealed class ReturningHeroObservationCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.weapon_drop";

        public string Name => "returning_hero_observation";

        public string Description => "Reports the current client view of one returning hero without mutating battle state.";

        public CoopCommandSide Side => CoopCommandSide.Client;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The returning player controller id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("WEAPON_DROP_RETURNING_HERO_OBSERVATION must run on a mission client");

            Mission mission = Mission.Current;
            CoopBattleController controller = mission?.GetMissionBehavior<CoopBattleController>();
            var unavailableReasons = new List<string>();
            var state = new ReturningHeroObservationState
            {
                Success = true,
                RequestedControllerId = args[0],
                AgentIndex = -1,
                MissionPresent = mission != null,
                CoopBattlePresent = controller != null,
                ObservationAvailable = mission != null && controller != null,
            };
            if (mission == null)
                unavailableReasons.Add("mission-unavailable");
            if (controller == null)
                unavailableReasons.Add("coop-battle-controller-unavailable");
            else
            {
                state.LocalControllerId = controller.Session.OwnControllerId;
                state.BattleInstanceId = controller.Session.InstanceId;
                state.DeploymentActivated = controller.Deployment.IsActivated;
                state.DeploymentCommitted = controller.Deployment.IsCommitted;
                var deploymentController = mission.GetMissionBehavior<DeploymentMissionController>();
                state.DeploymentControllerPresent = deploymentController != null;
                state.DeploymentTeamSetupOver = deploymentController?.TeamSetupOver ?? false;
                state.SupplierState = CoopTroopSupplierRegistry.CaptureDebugState(controller.Session.InstanceId);
                try
                {
                    state.PendingPuppetState = controller.CapturePendingPuppetState(args[0]);
                }
                catch (Exception e)
                {
                    state.PendingPuppetStateError = e.GetType().Name + ": " + e.Message;
                    unavailableReasons.Add("pending-puppet-state-unavailable");
                }
            }

            if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
                !ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            {
                state.ObservationAvailable = false;
                unavailableReasons.Add("campaign-services-unavailable");
                state.UnavailableReasons = unavailableReasons.ToArray();
                return Succeeded("LIVE_TEST_JSON=" + JsonConvert.SerializeObject(state));
            }

            state.CampaignServicesAvailable = true;
            if (!playerManager.TryGetPlayer(args[0], out var player))
            {
                unavailableReasons.Add("returning-player-unregistered");
                state.UnavailableReasons = unavailableReasons.ToArray();
                return Succeeded("LIVE_TEST_JSON=" + JsonConvert.SerializeObject(state));
            }

            state.PlayerRegistered = true;
            state.PlayerConnected = playerManager.IsConnected(player);
            state.RegisteredHeroId = player.HeroId;
            state.RegisteredPartyId = player.MobilePartyId;
            Hero hero = null;
            if (objectManager.TryGetObject(player.HeroId, out hero))
            {
                state.HeroResolved = true;
                state.HeroControlled = playerManager.Contains(hero);
                state.HeroStringId = hero.StringId;
            }
            if (objectManager.TryGetObject(player.MobilePartyId, out MobileParty party))
            {
                state.PartyResolved = true;
                state.PartyControlled = playerManager.Contains(party);
                state.PartyStringId = party.StringId;
                state.PartyActive = party.IsActive;
                if (party.MapEvent != null)
                {
                    state.PartyMapEventStringId = party.MapEvent.StringId;
                    if (objectManager.TryGetId(party.MapEvent, out string mapEventId))
                        state.PartyMapEventId = mapEventId;
                }
            }

            if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var agentRegistry))
            {
                unavailableReasons.Add("agent-registry-unavailable");
                state.UnavailableReasons = unavailableReasons.ToArray();
                return Succeeded("LIVE_TEST_JSON=" + JsonConvert.SerializeObject(state));
            }

            state.AgentRegistryAvailable = true;
            if (hero == null || !agentRegistry.TryGetHeroAgentInfo(hero, out CoopAgentInfo agentInfo))
            {
                unavailableReasons.Add("returning-hero-agent-unregistered");
                state.UnavailableReasons = unavailableReasons.ToArray();
                return Succeeded("LIVE_TEST_JSON=" + JsonConvert.SerializeObject(state));
            }

            Agent agent = agentInfo.Agent;
            state.HeroAgentRegistered = true;
            state.AgentId = agentInfo.AgentId.ToString("D");
            state.AgentActive = agent != null && agent.IsActive();
            state.AgentInCurrentMission = mission != null && agent != null && agent.Mission == mission;
            state.AgentIsMainAgent = ReferenceEquals(agent, Agent.Main);
            state.AgentIndex = agent?.Index ?? -1;
            state.AgentHealth = agent?.Health ?? 0f;
            state.CurrentAuthority = agentInfo.CurrentAuthority;
            state.OriginalOwner = agentInfo.OriginalOwner;
            state.MovementScopeId = agentInfo.MovementScopeId;
            state.MovementId = agentInfo.MovementId;
            state.AuthorityRevision = agentInfo.AuthorityRevision;
            if (agent?.Origin is PartyAgentOrigin origin)
            {
                state.OriginSeed = origin.Seed;
                if (origin.Party != null && objectManager.TryGetId(origin.Party, out string originPartyId))
                    state.OriginPartyId = originPartyId;
            }
            state.UnavailableReasons = unavailableReasons.ToArray();
            return Succeeded("LIVE_TEST_JSON=" + JsonConvert.SerializeObject(state));
        }
    }

    public sealed class DropLocalCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.weapon_drop";

        public string Name => "drop_local";

        public string Description => "Invokes the natural drop action for the locally controlled main agent.";

        public CoopCommandSide Side => CoopCommandSide.Client;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("equipment_slot", "The weapon equipment slot."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetActiveBattle(out Mission mission, out CoopBattleController controller, out string error))
                return Failed(error);
            if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var agentRegistry) ||
                !ContainerProvider.TryResolve<INetworkWorldItemRegistry>(out var worldItemRegistry) ||
                !ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            {
                return Failed("WEAPON_DROP_DROP_LOCAL required mission services are unavailable");
            }
            if (!TryParseEquipmentIndex(args[0], out EquipmentIndex equipmentIndex, out error))
                return Failed(error);

            Agent agent = Agent.Main;
            if (agent == null || !agent.IsActive() || agent.Mission != mission)
                return Failed("WEAPON_DROP_DROP_LOCAL no active main agent");
            if (!agentRegistry.TryGetAgentInfo(agent, out CoopAgentInfo agentInfo) ||
                !agentRegistry.IsLocallyControlled(agentInfo.AgentId))
            {
                return Failed("WEAPON_DROP_DROP_LOCAL main agent is not locally controlled");
            }
            if (agent.Equipment == null || agent.Equipment[equipmentIndex].IsEmpty)
                return Failed("WEAPON_DROP_DROP_LOCAL selected weapon slot is empty");
            if (!TryCaptureSnapshot(
                    mission,
                    controller,
                    agentRegistry,
                    worldItemRegistry,
                    objectManager,
                    agentInfo.AgentId,
                    equipmentIndex,
                    out SnapshotState snapshot,
                    out error))
            {
                return Failed(error);
            }

            agent.DropItem(equipmentIndex);
            snapshot.NativeDropInvoked = true;
            return Succeeded("LIVE_TEST_JSON=" + JsonConvert.SerializeObject(snapshot));
        }
    }

    public sealed class ArmCaptureCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.weapon_drop";

        public string Name => "arm_capture";

        public string Description => "Captures the next real network drop for one active battle agent slot.";

        public CoopCommandSide Side => CoopCommandSide.Client;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("agent_id", "The registered agent id."),
            new ExpectedArgs("equipment_slot", "The weapon equipment slot."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("WEAPON_DROP_ARM_CAPTURE must run on the natural origin client");
            if (!TryGetActiveBattle(out Mission mission, out CoopBattleController controller, out string error))
                return Failed(error);
            if (!Guid.TryParse(args[0], out Guid agentId) ||
                !TryParseEquipmentIndex(args[1], out EquipmentIndex equipmentIndex, out error))
            {
                return Failed(error ?? "WEAPON_DROP_ARM_CAPTURE agent id is invalid");
            }
            if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var agentRegistry) ||
                !agentRegistry.TryGetAgentInfo(agentId, out CoopAgentInfo agentInfo) ||
                agentInfo.Agent == null ||
                !agentInfo.Agent.IsActive() ||
                agentInfo.Agent.Mission != mission ||
                !ReferenceEquals(agentInfo.Agent, Agent.Main) ||
                !agentRegistry.IsLocallyControlled(agentId))
            {
                return Failed("WEAPON_DROP_ARM_CAPTURE target agent is not the local natural origin");
            }

            string battleInstanceId = Convert.ToString(controller.Session.InstanceId, CultureInfo.InvariantCulture);
            lock (CaptureGate)
            {
                capturedDrop = new CapturedDropState
                {
                    BattleInstanceId = battleInstanceId,
                    AgentId = agentId,
                    EquipmentIndex = equipmentIndex,
                    OriginControllerId = controller.Session.OwnControllerId,
                };
                DuplicateDrops.Clear();
            }
            return Succeeded("LIVE_TEST_JSON=" + JsonConvert.SerializeObject(
                GetReplayStatus(battleInstanceId, Guid.Empty, Guid.Empty)));
        }
    }

    public sealed class ReplayCapturedCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.weapon_drop";

        public string Name => "replay_captured";

        public string Description => "Replays the captured real network drop through the battle network.";

        public CoopCommandSide Side => CoopCommandSide.Client;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("send_count", "The number of ordered replays, from one through three."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("WEAPON_DROP_REPLAY_CAPTURED must run on the captured origin client");
            if (!TryGetActiveBattle(out _, out CoopBattleController controller, out string error))
                return Failed(error);
            if (!int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int sendCount) ||
                sendCount < 1 ||
                sendCount > 3)
            {
                return Failed("WEAPON_DROP_REPLAY_CAPTURED send count is invalid");
            }
            if (!ContainerProvider.TryResolve<IBattleNetwork>(out var network))
                return Failed("WEAPON_DROP_REPLAY_CAPTURED battle network is unavailable");

            string battleInstanceId = Convert.ToString(controller.Session.InstanceId, CultureInfo.InvariantCulture);
            NetworkWeaponDropped message;
            lock (CaptureGate)
            {
                if (capturedDrop == null ||
                    capturedDrop.Message == null ||
                    !string.Equals(capturedDrop.BattleInstanceId, battleInstanceId, StringComparison.Ordinal) ||
                    !string.Equals(
                        capturedDrop.OriginControllerId,
                        controller.Session.OwnControllerId,
                        StringComparison.Ordinal))
                {
                    return Failed("WEAPON_DROP_REPLAY_CAPTURED has no captured drop for the active battle");
                }
                message = capturedDrop.Message;
            }

            for (int index = 0; index < sendCount; index++)
                network.SendAll(message);

            lock (CaptureGate)
            {
                if (ReferenceEquals(capturedDrop?.Message, message))
                    capturedDrop.ReplaySendCount += sendCount;
            }
            return Succeeded("LIVE_TEST_JSON=" + JsonConvert.SerializeObject(
                GetReplayStatus(battleInstanceId, message.DropId, message.WorldItemId)));
        }
    }

    public sealed class ReplayStatusCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.weapon_drop";

        public string Name => "replay_status";

        public string Description => "Reports captured replay and duplicate already-applied evidence for one drop.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("drop_id", "The captured network drop id."),
            new ExpectedArgs("world_item_id", "The captured registered world item id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetActiveBattle(out _, out CoopBattleController controller, out string error))
                return Failed(error);
            if (!Guid.TryParse(args[0], out Guid dropId) ||
                !Guid.TryParse(args[1], out Guid worldItemId) ||
                dropId == Guid.Empty ||
                worldItemId == Guid.Empty)
            {
                return Failed("WEAPON_DROP_REPLAY_STATUS drop or world item id is invalid");
            }

            string battleInstanceId = Convert.ToString(controller.Session.InstanceId, CultureInfo.InvariantCulture);
            return Succeeded("LIVE_TEST_JSON=" + JsonConvert.SerializeObject(
                GetReplayStatus(battleInstanceId, dropId, worldItemId)));
        }
    }

    internal static void RecordOutgoingNaturalDrop(NetworkWeaponDropped message)
    {
        if (message == null ||
            message.IsCatchUp ||
            message.DropId == Guid.Empty ||
            message.WorldItemId == Guid.Empty ||
            message.AgentId == Guid.Empty ||
            string.IsNullOrEmpty(message.OriginControllerId) ||
            string.IsNullOrEmpty(message.ItemObjectId))
        {
            return;
        }

        lock (CaptureGate)
        {
            if (capturedDrop == null ||
                capturedDrop.Message != null ||
                capturedDrop.AgentId != message.AgentId ||
                capturedDrop.EquipmentIndex != message.EquipmentIndex ||
                !string.Equals(
                    capturedDrop.OriginControllerId,
                    message.OriginControllerId,
                    StringComparison.Ordinal))
            {
                return;
            }

            capturedDrop.Message = message;
            capturedDrop.CapturedAtOrigin = true;
            capturedDrop.CapturedOutgoingCount++;
        }
    }

    internal static void RecordDuplicateAlreadyApplied(NetworkWeaponDropped message)
    {
        if (message == null ||
            message.IsCatchUp ||
            message.DropId == Guid.Empty ||
            message.WorldItemId == Guid.Empty ||
            message.AgentId == Guid.Empty)
        {
            return;
        }

        lock (CaptureGate)
        {
            if (!DuplicateDrops.TryGetValue(message.DropId, out DuplicateDropState duplicate) ||
                duplicate.WorldItemId != message.WorldItemId)
            {
                duplicate = new DuplicateDropState
                {
                    DropId = message.DropId,
                    WorldItemId = message.WorldItemId,
                    AgentId = message.AgentId,
                    EquipmentIndex = message.EquipmentIndex,
                };
                DuplicateDrops[message.DropId] = duplicate;
            }
            duplicate.Count++;
        }
    }

    private static ReplayStatusState GetReplayStatus(
        string battleInstanceId,
        Guid expectedDropId,
        Guid expectedWorldItemId)
    {
        var status = new ReplayStatusState
        {
            Success = true,
            BattleInstanceId = battleInstanceId,
            ExpectedDropId = expectedDropId == Guid.Empty ? null : expectedDropId.ToString("D"),
            ExpectedWorldItemId = expectedWorldItemId == Guid.Empty ? null : expectedWorldItemId.ToString("D"),
        };

        lock (CaptureGate)
        {
            if (capturedDrop != null)
            {
                status.CaptureArmed = true;
                status.CapturedBattleInstanceId = capturedDrop.BattleInstanceId;
                status.CapturedCurrentBattle = string.Equals(
                    capturedDrop.BattleInstanceId,
                    battleInstanceId,
                    StringComparison.Ordinal);
                status.CapturedValidNetworkMessage = capturedDrop.Message != null;
                status.CapturedAtOrigin = capturedDrop.CapturedAtOrigin;
                status.CapturedOriginControllerId = capturedDrop.OriginControllerId;
                status.CapturedOutgoingCount = capturedDrop.CapturedOutgoingCount;
                status.ReplaySendCount = capturedDrop.ReplaySendCount;
                if (capturedDrop.Message != null)
                {
                    status.CapturedDropId = capturedDrop.Message.DropId.ToString("D");
                    status.CapturedWorldItemId = capturedDrop.Message.WorldItemId.ToString("D");
                    status.CapturedAgentId = capturedDrop.Message.AgentId.ToString("D");
                    status.CapturedEquipmentSlot = (int)capturedDrop.Message.EquipmentIndex;
                }
            }

            if (expectedDropId != Guid.Empty &&
                DuplicateDrops.TryGetValue(expectedDropId, out DuplicateDropState duplicate) &&
                duplicate.WorldItemId == expectedWorldItemId)
            {
                status.DuplicateAlreadyAppliedObserved = true;
                status.DuplicateAlreadyAppliedCount = duplicate.Count;
                status.DuplicateAgentId = duplicate.AgentId.ToString("D");
                status.DuplicateEquipmentSlot = (int)duplicate.EquipmentIndex;
            }
        }
        return status;
    }

    private static bool TryGetActiveBattle(
        out Mission mission,
        out CoopBattleController controller,
        out string error)
    {
        mission = Mission.Current;
        controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (mission == null || controller == null)
        {
            error = "WEAPON_DROP_SNAPSHOT no active coop battle";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryCaptureSnapshot(
        Mission mission,
        CoopBattleController controller,
        INetworkAgentRegistry agentRegistry,
        INetworkWorldItemRegistry worldItemRegistry,
        IObjectManager objectManager,
        Guid agentId,
        EquipmentIndex equipmentIndex,
        out SnapshotState snapshot,
        out string error)
    {
        snapshot = null;
        error = null;
        if (!agentRegistry.TryGetAgentInfo(agentId, out CoopAgentInfo agentInfo))
        {
            error = "WEAPON_DROP_SNAPSHOT agent is not registered";
            return false;
        }

        Agent agent = agentInfo.Agent;
        if (agent == null || !agent.IsActive() || agent.Mission != mission || agent.Equipment == null)
        {
            error = "WEAPON_DROP_SNAPSHOT target agent is unavailable";
            return false;
        }

        snapshot = new SnapshotState
        {
            Success = true,
            LocalRole = ModInformation.IsServer ? "server" : "client",
            LocalControllerId = controller.Session.OwnControllerId,
            BattleInstanceId = Convert.ToString(controller.Session.InstanceId, CultureInfo.InvariantCulture),
            AgentId = agentInfo.AgentId.ToString("D"),
            AgentIndex = agent.Index,
            IsMainAgent = ReferenceEquals(agent, Agent.Main),
            LocallyControlled = agentRegistry.IsLocallyControlled(agentInfo.AgentId),
            CurrentAuthority = agentInfo.CurrentAuthority,
            OriginalOwner = agentInfo.OriginalOwner,
            MovementScopeId = agentInfo.MovementScopeId,
            MovementId = agentInfo.MovementId,
            AuthorityRevision = agentInfo.AuthorityRevision,
            TeamSide = agent.Team?.Side.ToString(),
            EquipmentSlot = (int)equipmentIndex,
            EquipmentSlotName = equipmentIndex.ToString(),
            Slot = CaptureWeapon(agent.Equipment[equipmentIndex], objectManager),
            RegisteredControllerIds = agentRegistry.GetControllerIds()
                .OrderBy(controllerId => controllerId, StringComparer.Ordinal)
                .ToArray(),
            WorldItems = worldItemRegistry.GetAll()
                .OrderBy(pair => pair.Key)
                .Select(pair => CaptureWorldItem(pair.Key, pair.Value, objectManager))
                .ToArray(),
        };
        snapshot.RegisteredWorldItemCount = snapshot.WorldItems.Length;
        return true;
    }

    private static WeaponState CaptureWeapon(MissionWeapon weapon, IObjectManager objectManager)
    {
        var state = new WeaponState
        {
            IsEmpty = weapon.IsEmpty,
        };
        if (weapon.IsEmpty)
            return state;

        state.ItemObjectId = TryGetObjectId(objectManager, weapon.Item);
        state.ItemStringId = weapon.Item?.StringId;
        state.ModifierObjectId = TryGetObjectId(objectManager, weapon.ItemModifier);
        state.ModifierStringId = weapon.ItemModifier?.StringId;
        state.RawDataForNetwork = weapon.RawDataForNetwork;
        state.Amount = weapon.Amount;
        state.BannerCode = weapon.Banner?.Serialize();
        return state;
    }

    private static WorldItemState CaptureWorldItem(
        Guid worldItemId,
        SpawnedItemEntity item,
        IObjectManager objectManager)
    {
        if (item == null)
        {
            return new WorldItemState
            {
                WorldItemId = worldItemId.ToString("D"),
            };
        }

        return new WorldItemState
        {
            WorldItemId = worldItemId.ToString("D"),
            MissionObjectId = item.Id.Id,
            CreatedAtRuntime = item.Id.CreatedAtRuntime,
            IsRemoved = item.IsRemoved,
            IsDeactivated = item.IsDeactivated,
            GameEntityValid = item.GameEntity != null && item.GameEntity.IsValid,
            Weapon = CaptureWeapon(item.WeaponCopy, objectManager),
        };
    }

    private static string TryGetObjectId(IObjectManager objectManager, object value)
    {
        return value != null && objectManager.TryGetId(value, out string id)
            ? id
            : null;
    }

    private static bool TryParseEquipmentIndex(
        string value,
        out EquipmentIndex equipmentIndex,
        out string error)
    {
        equipmentIndex = EquipmentIndex.None;
        error = null;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) ||
            index < (int)EquipmentIndex.WeaponItemBeginSlot ||
            index >= (int)EquipmentIndex.NumAllWeaponSlots)
        {
            error = "WEAPON_DROP_SNAPSHOT equipment slot is invalid";
            return false;
        }

        equipmentIndex = (EquipmentIndex)index;
        return true;
    }
}
#endif
