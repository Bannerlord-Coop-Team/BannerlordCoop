#if DEBUG
using Common;
using Common.Commands;
using GameInterface;
using GameInterface.Services.ObjectManager;
using Missions.Battles;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Linq;
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
