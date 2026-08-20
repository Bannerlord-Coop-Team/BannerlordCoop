#if DEBUG
using Common;
using Common.Messaging;
using GameInterface;
using GameInterface.Services.ObjectManager;
using Missions.Agents.Messages;
using Missions.Agents.Packets;
using Missions.Battles;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace Missions.Agents;

/// <summary>
/// Reversible live-test commands for exercising Bannerlord's real dropped-item pickup path in a co-op battle.
/// </summary>
internal static class WeaponPickupDebugCommands
{
    private sealed class PickupFixture
    {
        public Agent Agent { get; set; }
        public Guid AgentId { get; set; }
        public EquipmentIndex Slot { get; set; }
        public MissionWeapon OriginalWeapon { get; set; }
        public AgentEquipmentData OriginalEquipment { get; set; }
        public string ItemId { get; set; }
        public Guid WorldItemId { get; set; }
        public SpawnedItemEntity DroppedItem { get; set; }
        public bool PickupAttempted { get; set; }
        public string Phase { get; set; }
        public bool PartialConsumable { get; set; }
        public EquipmentIndex SourceSlot { get; set; }
        public short OriginalSourceAmount { get; set; }
        public short PreparedSourceAmount { get; set; }
        public short DroppedAmount { get; set; }
    }

    private sealed class FixtureLifetimeBehavior : MissionBehavior
    {
        private readonly PickupFixture ownedFixture;

        public FixtureLifetimeBehavior(PickupFixture ownedFixture)
        {
            this.ownedFixture = ownedFixture;
        }

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnRemoveBehavior()
        {
            ClearFixture(ownedFixture);
        }
    }

    private sealed class AgentCameraBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnPreDisplayMissionTick(float dt)
        {
            UpdateCameraFrame();
        }

        public override void OnRemoveBehavior()
        {
            if (ReferenceEquals(cameraBehavior, this))
                cameraBehavior = null;
            ReleaseCamera();
        }
    }

    private static PickupFixture fixture;
    private static AgentCameraBehavior cameraBehavior;
    private static Camera agentCamera;
    private static MatrixFrame agentCameraLocalFrame;
    private static Agent focusedAgent;
    private static Guid focusedAgentId;
    private static string focusedView = "none";

    [CommandLineArgumentFunction("state", "coop.debug.weapon_pickup")]
    public static string State(List<string> args)
    {
        if (args.Count > 1)
            return "Usage: coop.debug.weapon_pickup.state [agentId]";
        if (!TryResolveBattleAgent(args, out var registry, out var info, out var error))
            return error;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "WEAPON_PICKUP_STATE error=object-manager-unavailable";
        if (!ContainerProvider.TryResolve<INetworkWorldItemRegistry>(out var worldItemRegistry))
            return "WEAPON_PICKUP_STATE error=world-item-registry-unavailable";

        Agent agent = info.Agent;
        var slotStates = Enumerable.Range(
                (int)EquipmentIndex.WeaponItemBeginSlot,
                (int)EquipmentIndex.NumAllWeaponSlots - (int)EquipmentIndex.WeaponItemBeginSlot)
            .Select(index =>
            {
                var slot = (EquipmentIndex)index;
                MissionWeapon weapon = agent.Equipment[slot];
                return new
                {
                    index,
                    item = GetItemId(objectManager, weapon.Item),
                    amount = GetWeaponAmount(weapon),
                };
            })
            .ToArray();
        var worldItems = worldItemRegistry.GetAll()
            .Select(pair => new
            {
                id = pair.Key.ToString("N"),
                item = GetItemId(objectManager, pair.Value?.WeaponCopy.Item),
                amount = pair.Value == null ? 0 : pair.Value.WeaponCopy.Amount,
                active = pair.Value != null && !IsDroppedItemInactive(pair.Value),
            })
            .OrderBy(item => item.id)
            .ToArray();
        string slots = string.Join(",", slotStates.Select(slot => $"{slot.index}:{slot.item}"));
        string amounts = string.Join(",", slotStates.Select(slot => $"{slot.index}:{slot.amount}"));
        string fixturePhase = fixture == null
            ? "inactive"
            : fixture.AgentId == info.AgentId ? fixture.Phase : "other-agent";
        bool worldItemActive = fixture == null || fixture.AgentId != info.AgentId
            ? false
            : fixture.DroppedItem != null && !fixture.DroppedItem.IsDeactivated;
        bool fieldBattle = MobileParty.MainParty?.MapEvent?.IsFieldBattle == true;
        bool siegeAssault = MobileParty.MainParty?.MapEvent?.IsSiegeAssault == true;
        CoopBattleController controller = Mission.Current.GetMissionBehavior<CoopBattleController>();
        EquipmentIndex candidateSlot = fixture?.AgentId == info.AgentId
            ? fixture.Slot
            : agent.GetPrimaryWieldedItemIndex();
        if (!IsPopulatedWeaponSlot(agent, candidateSlot))
        {
            candidateSlot = Enumerable.Range(
                    (int)EquipmentIndex.WeaponItemBeginSlot,
                    (int)EquipmentIndex.ExtraWeaponSlot - (int)EquipmentIndex.WeaponItemBeginSlot)
                .Select(index => (EquipmentIndex)index)
                .FirstOrDefault(slot => IsPopulatedWeaponSlot(agent, slot));
        }
        string candidateItem = fixture?.AgentId == info.AgentId
            ? fixture.ItemId
            : IsPopulatedWeaponSlot(agent, candidateSlot)
                ? GetItemId(objectManager, agent.Equipment[candidateSlot].Item)
                : null;
        string structuredState = JsonConvert.SerializeObject(new
        {
            fieldBattle,
            siegeAssault,
            instanceId = controller.Session.InstanceId,
            ownControllerId = controller.Session.OwnControllerId,
            isBattleHost = controller.Session.IsLocalHost,
            agentId = info.AgentId.ToString("N"),
            authority = info.CurrentAuthority,
            originalOwner = info.OriginalOwner,
            locallyControlled = registry.IsLocallyControlled(info.AgentId),
            active = agent.IsActive(),
            primaryWieldedSlot = (int)agent.GetPrimaryWieldedItemIndex(),
            offhandWieldedSlot = (int)agent.GetOffhandWieldedItemIndex(),
            slots = slotStates,
            worldItems,
            fixture = fixturePhase,
            fixtureSlot = candidateItem == null ? -1 : (int)candidateSlot,
            fixtureItem = candidateItem,
            fixtureWorldItemId = fixture?.AgentId == info.AgentId
                ? fixture.WorldItemId.ToString("N")
                : null,
            worldItemActive,
        });

        return $"WEAPON_PICKUP_STATE fieldBattle={fieldBattle} agent={info.AgentId:N} " +
            $"authority={info.CurrentAuthority} originalOwner={info.OriginalOwner} " +
            $"local={registry.IsLocallyControlled(info.AgentId)} active={agent.IsActive()} " +
            $"main={(int)agent.GetPrimaryWieldedItemIndex()} off={(int)agent.GetOffhandWieldedItemIndex()} " +
            $"slots={slots} amounts={amounts} fixture={fixturePhase} worldItemActive={worldItemActive}" +
            Environment.NewLine + "LIVE_TEST_JSON=" + structuredState;
    }

    [CommandLineArgumentFunction("fixture_drop", "coop.debug.weapon_pickup")]
    public static string DropFixtureWeapon(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.weapon_pickup.fixture_drop";
        if (fixture != null)
            return $"WEAPON_PICKUP_DROP error=fixture-active phase={fixture.Phase}";
        if (!TryResolveLocalMainAgent(out var registry, out var info, out var error))
            return "WEAPON_PICKUP_DROP error=" + error;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "WEAPON_PICKUP_DROP error=object-manager-unavailable";
        if (!ContainerProvider.TryResolve<INetworkWorldItemRegistry>(out var worldItemRegistry))
            return "WEAPON_PICKUP_DROP error=world-item-registry-unavailable";

        Agent agent = info.Agent;
        EquipmentIndex slot = agent.GetPrimaryWieldedItemIndex();
        if (!IsPopulatedWeaponSlot(agent, slot))
        {
            slot = EquipmentIndex.None;
            for (EquipmentIndex candidate = EquipmentIndex.WeaponItemBeginSlot;
                 candidate < EquipmentIndex.ExtraWeaponSlot;
                 candidate++)
            {
                if (!IsPopulatedWeaponSlot(agent, candidate)) continue;
                slot = candidate;
                break;
            }
        }
        if (!IsPopulatedWeaponSlot(agent, slot))
            return "WEAPON_PICKUP_DROP error=no-equipped-weapon";

        MissionWeapon originalWeapon = agent.Equipment[slot];
        string itemId = GetItemId(objectManager, originalWeapon.Item);
        if (itemId.StartsWith("unregistered:", StringComparison.Ordinal))
            return $"WEAPON_PICKUP_DROP error=item-unregistered item={itemId}";

        var newFixture = new PickupFixture
        {
            Agent = agent,
            AgentId = info.AgentId,
            Slot = slot,
            OriginalWeapon = originalWeapon,
            OriginalEquipment = new AgentEquipmentData(agent),
            ItemId = itemId,
            Phase = "dropping",
        };
        fixture = newFixture;

        HashSet<SpawnedItemEntity> before = WeaponDropItemTracker.Capture();
        agent.DropItem(slot);
        SpawnedItemEntity droppedItem = WeaponDropItemTracker.FindDroppedItem(before);
        if (droppedItem == null)
        {
            MissionWeapon restoreWeapon = originalWeapon;
            agent.EquipWeaponWithNewEntity(slot, ref restoreWeapon);
            newFixture.OriginalEquipment.Apply(agent);
            fixture = null;
            return "WEAPON_PICKUP_DROP error=world-item-not-created restored=True";
        }

        newFixture.DroppedItem = droppedItem;
        newFixture.Phase = "dropped";
        agent.Mission.AddMissionBehavior(new FixtureLifetimeBehavior(newFixture));
        Guid worldItemId = worldItemRegistry.GetAll()
            .Where(pair => ReferenceEquals(pair.Value, droppedItem))
            .Select(pair => pair.Key)
            .SingleOrDefault();
        if (worldItemId == Guid.Empty)
        {
            droppedItem.OnUseStopped(agent, isSuccessful: true, (int)slot);
            if (!IsPopulatedWeaponSlot(agent, slot))
            {
                MissionWeapon restoreWeapon = originalWeapon;
                agent.EquipWeaponWithNewEntity(slot, ref restoreWeapon);
            }
            newFixture.OriginalEquipment.Apply(agent);
            bool restored = IsPopulatedWeaponSlot(agent, slot) &&
                GetItemId(objectManager, agent.Equipment[slot].Item) == itemId &&
                IsDroppedItemInactive(droppedItem);
            ClearFixture(newFixture);
            return $"WEAPON_PICKUP_DROP error=world-item-unregistered restored={restored}";
        }
        newFixture.WorldItemId = worldItemId;
        return $"WEAPON_PICKUP_DROPPED agent={newFixture.AgentId:N} slot={(int)slot} " +
            $"item={itemId} worldItem={worldItemId:N} worldItemActive={!droppedItem.IsDeactivated}";
    }

    [CommandLineArgumentFunction("fixture_pickup", "coop.debug.weapon_pickup")]
    public static string PickupFixtureWeapon(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.weapon_pickup.fixture_pickup";
        if (fixture == null || fixture.Phase != "dropped")
            return $"WEAPON_PICKUP_PICKUP error=fixture-not-dropped phase={fixture?.Phase ?? "inactive"}";
        if (fixture.DroppedItem == null || fixture.DroppedItem.IsDeactivated)
            return "WEAPON_PICKUP_PICKUP error=world-item-unavailable";

        fixture.PickupAttempted = true;
        fixture.Phase = "picking";
        fixture.DroppedItem.OnUseStopped(fixture.Agent, isSuccessful: true, (int)fixture.Slot);
        fixture.Phase = "picked";

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "WEAPON_PICKUP_PICKUP error=object-manager-unavailable";
        string currentItem = GetItemId(objectManager, fixture.Agent.Equipment[fixture.Slot].Item);
        if (currentItem != fixture.ItemId)
            return $"WEAPON_PICKUP_PICKUP error=local-item-mismatch expected={fixture.ItemId} actual={currentItem}";

        return $"WEAPON_PICKUP_PICKED agent={fixture.AgentId:N} slot={(int)fixture.Slot} " +
            $"item={fixture.ItemId} worldItem={fixture.WorldItemId:N} " +
            $"main={(int)fixture.Agent.GetPrimaryWieldedItemIndex()} " +
            $"off={(int)fixture.Agent.GetOffhandWieldedItemIndex()}";
    }

    [CommandLineArgumentFunction("partial_fixture_drop", "coop.debug.weapon_pickup")]
    public static string DropPartialConsumableFixture(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.weapon_pickup.partial_fixture_drop";
        if (fixture != null)
            return $"WEAPON_PICKUP_PARTIAL_DROP error=fixture-active phase={fixture.Phase}";
        if (!TryResolveLocalMainAgent(out _, out var info, out var error))
            return "WEAPON_PICKUP_PARTIAL_DROP error=" + error;
        if (!ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker))
            return "WEAPON_PICKUP_PARTIAL_DROP error=message-broker-unavailable";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "WEAPON_PICKUP_PARTIAL_DROP error=object-manager-unavailable";
        if (!ContainerProvider.TryResolve<INetworkWorldItemRegistry>(out var worldItemRegistry))
            return "WEAPON_PICKUP_PARTIAL_DROP error=world-item-registry-unavailable";

        var preparation = new PreparePartialConsumablePickupFixture(info.Agent);
        messageBroker.Publish(info.Agent, preparation);
        if (!preparation.Handled)
            return "WEAPON_PICKUP_PARTIAL_DROP error=fixture-handler-unavailable";
        if (!preparation.Succeeded)
            return "WEAPON_PICKUP_PARTIAL_DROP error=" + (preparation.Error ?? "fixture-prepare-failed");

        var newFixture = new PickupFixture
        {
            Agent = info.Agent,
            AgentId = info.AgentId,
            Slot = preparation.DropSlot,
            SourceSlot = preparation.SourceSlot,
            ItemId = preparation.ItemObjectId,
            OriginalSourceAmount = preparation.OriginalSourceAmount,
            PreparedSourceAmount = preparation.SourceAmount,
            DroppedAmount = preparation.DroppedAmount,
            PartialConsumable = true,
            Phase = "dropping",
        };
        fixture = newFixture;

        HashSet<SpawnedItemEntity> before = WeaponDropItemTracker.Capture();
        info.Agent.DropItem(preparation.DropSlot);
        SpawnedItemEntity droppedItem = WeaponDropItemTracker.FindDroppedItem(before);
        if (droppedItem == null)
        {
            bool restored = TryRollbackPartialConsumableDrop(
                messageBroker,
                worldItemRegistry,
                newFixture,
                Guid.Empty,
                out string rollbackError);
            return $"WEAPON_PICKUP_PARTIAL_DROP error=world-item-not-created " +
                $"restored={restored} rollbackError={rollbackError ?? "none"}";
        }

        newFixture.DroppedItem = droppedItem;
        newFixture.Phase = "dropped";
        info.Agent.Mission.AddMissionBehavior(new FixtureLifetimeBehavior(newFixture));
        Guid worldItemId = worldItemRegistry.GetAll()
            .Where(pair => ReferenceEquals(pair.Value, droppedItem))
            .Select(pair => pair.Key)
            .SingleOrDefault();
        newFixture.WorldItemId = worldItemId;
        if (droppedItem.WeaponCopy.Amount != preparation.DroppedAmount)
        {
            bool restored = TryRollbackPartialConsumableDrop(
                messageBroker,
                worldItemRegistry,
                newFixture,
                worldItemId,
                out string rollbackError);
            return $"WEAPON_PICKUP_PARTIAL_DROP error=world-item-amount-mismatch " +
                $"expected={preparation.DroppedAmount} actual={droppedItem.WeaponCopy.Amount} " +
                $"restored={restored} rollbackError={rollbackError ?? "none"}";
        }
        if (worldItemId == Guid.Empty)
        {
            bool restored = TryRollbackPartialConsumableDrop(
                messageBroker,
                worldItemRegistry,
                newFixture,
                Guid.Empty,
                out string rollbackError);
            return $"WEAPON_PICKUP_PARTIAL_DROP error=world-item-unregistered " +
                $"restored={restored} rollbackError={rollbackError ?? "none"}";
        }

        string itemId = GetItemId(objectManager, droppedItem.WeaponCopy.Item);
        return $"WEAPON_PICKUP_PARTIAL_DROPPED agent={newFixture.AgentId:N} " +
            $"sourceSlot={(int)newFixture.SourceSlot} dropSlot={(int)newFixture.Slot} item={itemId} " +
            $"sourceBefore={newFixture.PreparedSourceAmount} worldBefore={droppedItem.WeaponCopy.Amount} " +
            $"worldItem={worldItemId:N} worldItemActive={!droppedItem.IsDeactivated}";
    }

    [CommandLineArgumentFunction("partial_fixture_pickup", "coop.debug.weapon_pickup")]
    public static string PickupPartialConsumableFixture(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.weapon_pickup.partial_fixture_pickup";
        if (fixture == null || !fixture.PartialConsumable || fixture.Phase != "dropped")
        {
            return $"WEAPON_PICKUP_PARTIAL_PICKUP error=fixture-not-dropped " +
                $"phase={fixture?.Phase ?? "inactive"}";
        }
        if (fixture.DroppedItem == null || fixture.DroppedItem.IsDeactivated)
            return "WEAPON_PICKUP_PARTIAL_PICKUP error=world-item-unavailable";

        short sourceBefore = GetWeaponAmount(fixture.Agent.Equipment[fixture.SourceSlot]);
        short worldBefore = fixture.DroppedItem.WeaponCopy.Amount;
        fixture.PickupAttempted = true;
        fixture.Phase = "picking";
        fixture.DroppedItem.OnUseStopped(
            fixture.Agent,
            isSuccessful: true,
            (int)fixture.SourceSlot);
        fixture.Phase = "picked";

        short sourceAfter = GetWeaponAmount(fixture.Agent.Equipment[fixture.SourceSlot]);
        short worldAfter = fixture.DroppedItem.WeaponCopy.Amount;
        if (sourceAfter != 32 || worldAfter <= 0 || worldAfter >= worldBefore ||
            fixture.DroppedItem.IsDeactivated)
        {
            return $"WEAPON_PICKUP_PARTIAL_PICKUP error=unexpected-local-result " +
                $"sourceBefore={sourceBefore} sourceAfter={sourceAfter} " +
                $"worldBefore={worldBefore} worldAfter={worldAfter} " +
                $"worldItemActive={!fixture.DroppedItem.IsDeactivated}";
        }

        return $"WEAPON_PICKUP_PARTIAL_PICKED agent={fixture.AgentId:N} " +
            $"sourceSlot={(int)fixture.SourceSlot} dropSlot={(int)fixture.Slot} item={fixture.ItemId} " +
            $"worldItem={fixture.WorldItemId:N} sourceBefore={sourceBefore} sourceAfter={sourceAfter} " +
            $"worldBefore={worldBefore} worldAfter={worldAfter} " +
            $"worldItemActive={!fixture.DroppedItem.IsDeactivated}";
    }

    [CommandLineArgumentFunction("world_item_state", "coop.debug.weapon_pickup")]
    public static string WorldItemState(List<string> args)
    {
        if (args.Count != 1 || !Guid.TryParse(args[0], out Guid worldItemId))
            return "Usage: coop.debug.weapon_pickup.world_item_state <worldItemId>";
        if (!ContainerProvider.TryResolve<INetworkWorldItemRegistry>(out var worldItemRegistry))
            return "WEAPON_PICKUP_WORLD_ITEM error=registry-unavailable";
        if (!worldItemRegistry.TryGet(worldItemId, out var worldItem))
            return $"WEAPON_PICKUP_WORLD_ITEM id={worldItemId:N} registered=False";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "WEAPON_PICKUP_WORLD_ITEM error=object-manager-unavailable";

        bool inactive = IsDroppedItemInactive(worldItem);
        return $"WEAPON_PICKUP_WORLD_ITEM id={worldItemId:N} registered=True " +
            $"active={!inactive} deactivated={worldItem.IsDeactivated} removed={worldItem.IsRemoved} " +
            $"item={GetItemId(objectManager, worldItem.WeaponCopy.Item)} amount={worldItem.WeaponCopy.Amount}";
    }

    [CommandLineArgumentFunction("fixture_restore", "coop.debug.weapon_pickup")]
    public static string RestoreFixtureWeapon(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.weapon_pickup.fixture_restore";
        if (fixture == null)
            return "WEAPON_PICKUP_RESTORED fixture=inactive";
        if (fixture.PartialConsumable)
            return RestorePartialConsumableFixture();
        if (fixture.Agent == null || !fixture.Agent.IsActive() || fixture.Agent.Mission != Mission.Current)
        {
            PickupFixture unavailableFixture = fixture;
            ClearFixture(unavailableFixture);
            return "WEAPON_PICKUP_RESTORE error=agent-unavailable fixtureCleared=True mission-ended=True";
        }
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "WEAPON_PICKUP_RESTORE error=object-manager-unavailable";

        PickupFixture currentFixture = fixture;
        Agent agent = currentFixture.Agent;
        string currentItem = GetItemId(objectManager, agent.Equipment[currentFixture.Slot].Item);
        if (currentItem == "none" && !currentFixture.PickupAttempted &&
            !IsDroppedItemInactive(currentFixture.DroppedItem))
        {
            currentFixture.DroppedItem.OnUseStopped(agent, isSuccessful: true, (int)currentFixture.Slot);
            currentItem = GetItemId(objectManager, agent.Equipment[currentFixture.Slot].Item);
        }
        if (!TryRemoveActiveDroppedItem(currentFixture.DroppedItem, out string removalError))
            return "WEAPON_PICKUP_RESTORE error=" + removalError;
        if (currentItem == "none")
        {
            MissionWeapon restoreWeapon = currentFixture.OriginalWeapon;
            agent.EquipWeaponWithNewEntity(currentFixture.Slot, ref restoreWeapon);
            currentItem = GetItemId(objectManager, agent.Equipment[currentFixture.Slot].Item);
        }
        if (currentItem != currentFixture.ItemId)
            return $"WEAPON_PICKUP_RESTORE error=item-mismatch expected={currentFixture.ItemId} actual={currentItem}";

        currentFixture.OriginalEquipment.Apply(agent);
        currentItem = GetItemId(objectManager, agent.Equipment[currentFixture.Slot].Item);
        if (currentItem != currentFixture.ItemId)
            return $"WEAPON_PICKUP_RESTORE error=original-equipment-mismatch expected={currentFixture.ItemId} actual={currentItem}";
        if (!IsDroppedItemInactive(currentFixture.DroppedItem))
            return "WEAPON_PICKUP_RESTORE error=world-item-still-active";

        Guid agentId = currentFixture.AgentId;
        EquipmentIndex slot = currentFixture.Slot;
        string itemId = currentFixture.ItemId;
        ClearFixture(currentFixture);
        return $"WEAPON_PICKUP_RESTORED agent={agentId:N} slot={(int)slot} item={itemId} " +
            "worldItemInactive=True";
    }

    private static string RestorePartialConsumableFixture()
    {
        if (!ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker))
            return "WEAPON_PICKUP_RESTORE error=message-broker-unavailable";

        PickupFixture currentFixture = fixture;
        if (!TryRestorePartialConsumableFixture(
                messageBroker,
                currentFixture,
                currentFixture.WorldItemId,
                out RestorePartialConsumablePickupFixture restoration))
        {
            return "WEAPON_PICKUP_RESTORE error=" +
                (restoration?.Error ?? "partial-fixture-restore-failed");
        }

        Guid agentId = currentFixture.AgentId;
        EquipmentIndex sourceSlot = currentFixture.SourceSlot;
        EquipmentIndex dropSlot = currentFixture.Slot;
        string itemId = currentFixture.ItemId;
        ClearFixture(currentFixture);
        return $"WEAPON_PICKUP_PARTIAL_RESTORED agent={agentId:N} " +
            $"sourceSlot={(int)sourceSlot} dropSlot={(int)dropSlot} item={itemId} " +
            $"sourceAmount={restoration.RestoredSourceAmount} dropSlotEmpty={restoration.DropSlotEmpty} " +
            $"worldItemInactive={restoration.WorldItemInactive}";
    }

    private static bool TryRestorePartialConsumableFixture(
        IMessageBroker messageBroker,
        PickupFixture currentFixture,
        Guid worldItemId,
        out RestorePartialConsumablePickupFixture restoration)
    {
        restoration = new RestorePartialConsumablePickupFixture(
            currentFixture.Agent,
            currentFixture.AgentId,
            worldItemId);
        messageBroker.Publish(currentFixture.Agent, restoration);
        return restoration.Handled && restoration.Succeeded;
    }

    private static bool TryRollbackPartialConsumableDrop(
        IMessageBroker messageBroker,
        INetworkWorldItemRegistry worldItemRegistry,
        PickupFixture currentFixture,
        Guid worldItemId,
        out string error)
    {
        error = null;
        if (worldItemId == Guid.Empty)
            worldItemRegistry.TryGetId(currentFixture.DroppedItem, out worldItemId);

        bool removed = TryRemoveActiveDroppedItem(currentFixture.DroppedItem, out string removalError);
        if (!TryRestorePartialConsumableFixture(
                messageBroker,
                currentFixture,
                worldItemId,
                out RestorePartialConsumablePickupFixture restoration))
        {
            error = restoration?.Error ?? "partial-fixture-restore-failed";
            return false;
        }

        if (!removed && !IsDroppedItemInactive(currentFixture.DroppedItem))
        {
            error = removalError ?? "world-item-removal-failed";
            return false;
        }
        if (worldItemId != Guid.Empty)
            worldItemRegistry.Remove(worldItemId);

        ClearFixture(currentFixture);
        return true;
    }

    [CommandLineArgumentFunction("focus_agent", "coop.debug.weapon_pickup")]
    public static string FocusAgent(List<string> args)
    {
        if (args.Count < 1 || args.Count > 2 || !Guid.TryParse(args[0], out Guid agentId))
            return "Usage: coop.debug.weapon_pickup.focus_agent <agentId> [left|right|wide]";

        string view = args.Count == 2 ? args[1].ToLowerInvariant() : "left";
        if (view != "left" && view != "right" && view != "wide")
            return "Usage: coop.debug.weapon_pickup.focus_agent <agentId> [left|right|wide]";
        if (!TryResolveBattleAgent(new List<string> { agentId.ToString("N") }, out _, out var info, out var error))
            return error;
        if (!(ScreenManager.TopScreen is MissionScreen missionScreen) || missionScreen.CombatCamera == null)
            return "WEAPON_PICKUP_CAMERA error=mission-screen-unavailable";

        Agent agent = info.Agent;
        GameEntity visualEntity = agent.AgentVisuals?.GetEntity();
        if (ReferenceEquals(visualEntity, null))
            return $"WEAPON_PICKUP_CAMERA error=agent-visual-unavailable agent={agentId:N}";

        ReleaseCamera();
        agentCamera = Camera.CreateCamera();
        agentCamera.FillParametersFrom(missionScreen.CombatCamera);
        Vec3 target = new Vec3(0f, 0f, 1.1f);
        Vec3 position;
        switch (view)
        {
            case "right":
                position = new Vec3(3.2f, -5.5f, 2.8f);
                break;
            case "wide":
                position = new Vec3(-5.5f, -9.5f, 4.4f);
                break;
            default:
                position = new Vec3(-3.2f, -5.5f, 2.8f);
                break;
        }
        agentCamera.LookAt(position, target, Vec3.Up);
        agentCameraLocalFrame = agentCamera.Frame;
        agentCamera.Entity = GameEntity.CreateEmpty(
            Mission.Current.Scene,
            isModifiableFromEditor: false,
            createPhysics: false,
            callScriptCallbacks: false);
        focusedAgent = agent;
        focusedAgentId = agentId;
        focusedView = view;
        EnsureCameraBehavior(Mission.Current);
        UpdateCameraFrame();
        missionScreen.CustomCamera = agentCamera;
        return $"WEAPON_PICKUP_CAMERA_FOCUSED agent={agentId:N} view={view}";
    }

    [CommandLineArgumentFunction("camera_state", "coop.debug.weapon_pickup")]
    public static string CameraState(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.weapon_pickup.camera_state";
        if (ReferenceEquals(agentCamera, null) || ReferenceEquals(agentCamera.Entity, null) ||
            ReferenceEquals(focusedAgent, null) || !focusedAgent.IsActive() ||
            !(ScreenManager.TopScreen is MissionScreen missionScreen) ||
            missionScreen.CombatCamera == null)
        {
            return "WEAPON_PICKUP_CAMERA_STATE active=False";
        }

        UpdateCameraFrame();
        MatrixFrame cameraEntityFrame = agentCamera.Entity.GetGlobalFrame();
        Vec3 renderedPosition = missionScreen.CombatCamera.Position;
        Vec3 entityDirection = -cameraEntityFrame.rotation.u;
        entityDirection.Normalize();
        Vec3 renderedDirection = missionScreen.CombatCamera.Direction;
        float directionDot =
            (renderedDirection.X * entityDirection.X) +
            (renderedDirection.Y * entityDirection.Y) +
            (renderedDirection.Z * entityDirection.Z);
        float positionDelta = (renderedPosition - cameraEntityFrame.origin).Length;
        bool active = ReferenceEquals(missionScreen.CustomCamera, agentCamera);
        return string.Format(
            CultureInfo.InvariantCulture,
            "WEAPON_PICKUP_CAMERA_STATE active={0} agent={1:N} view={2} positionDelta={3:F3} directionDot={4:F3}",
            active,
            focusedAgentId,
            focusedView,
            positionDelta,
            directionDot);
    }

    [CommandLineArgumentFunction("release_camera", "coop.debug.weapon_pickup")]
    public static string ReleaseAgentCamera(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.weapon_pickup.release_camera";
        bool released = ReleaseCamera();
        return released ? "WEAPON_PICKUP_CAMERA_RELEASED" : "WEAPON_PICKUP_CAMERA_RELEASED already=False";
    }

    private static bool TryResolveLocalMainAgent(
        out INetworkAgentRegistry registry,
        out CoopAgentInfo info,
        out string error)
    {
        registry = null;
        info = null;
        error = null;
        Agent agent = Agent.Main;
        if (Mission.Current?.GetMissionBehavior<CoopBattleController>() == null)
        {
            error = "not-a-coop-battle";
            return false;
        }
        if (agent == null || !agent.IsActive() || agent.Mission != Mission.Current)
        {
            error = "main-agent-unavailable";
            return false;
        }
        if (!ContainerProvider.TryResolve(out registry) ||
            !registry.TryGetAgentInfo(agent, out info) ||
            !registry.IsLocallyControlled(info.AgentId))
        {
            error = "main-agent-not-locally-controlled";
            return false;
        }
        return true;
    }

    private static bool TryResolveBattleAgent(
        List<string> args,
        out INetworkAgentRegistry registry,
        out CoopAgentInfo info,
        out string error)
    {
        registry = null;
        info = null;
        error = null;
        if (Mission.Current?.GetMissionBehavior<CoopBattleController>() == null)
        {
            error = "WEAPON_PICKUP_STATE error=not-a-coop-battle";
            return false;
        }
        if (!ContainerProvider.TryResolve(out registry))
        {
            error = "WEAPON_PICKUP_STATE error=registry-unavailable";
            return false;
        }

        if (args.Count == 0)
        {
            Agent main = Agent.Main;
            if (main == null || !registry.TryGetAgentInfo(main, out info))
            {
                error = "WEAPON_PICKUP_STATE error=main-agent-unavailable";
                return false;
            }
        }
        else if (!Guid.TryParse(args[0], out Guid agentId) || !registry.TryGetAgentInfo(agentId, out info))
        {
            error = $"WEAPON_PICKUP_STATE error=agent-not-found agent={args[0]}";
            return false;
        }

        if (info.Agent == null || !info.Agent.IsActive() || info.Agent.Mission != Mission.Current)
        {
            error = $"WEAPON_PICKUP_STATE error=agent-inactive agent={info.AgentId:N}";
            return false;
        }
        return true;
    }

    private static bool IsPopulatedWeaponSlot(Agent agent, EquipmentIndex slot)
    {
        return slot >= EquipmentIndex.WeaponItemBeginSlot &&
            slot < EquipmentIndex.ExtraWeaponSlot &&
            agent.Equipment[slot].Item != null;
    }

    private static string GetItemId(IObjectManager objectManager, ItemObject item)
    {
        if (item == null) return "none";
        return objectManager.TryGetId(item, out string itemId)
            ? itemId
            : "unregistered:" + (item.StringId ?? "unknown");
    }

    private static short GetWeaponAmount(MissionWeapon weapon)
    {
        return weapon.IsEmpty ? (short)0 : weapon.Amount;
    }

    private static bool IsDroppedItemInactive(SpawnedItemEntity droppedItem)
    {
        return droppedItem == null || droppedItem.IsRemoved || droppedItem.IsDeactivated;
    }

    private static bool TryRemoveActiveDroppedItem(SpawnedItemEntity droppedItem, out string error)
    {
        error = null;
        if (IsDroppedItemInactive(droppedItem)) return true;
        if (!droppedItem.GameEntity.IsValid)
        {
            error = "world-item-entity-invalid";
            return false;
        }

        droppedItem.GameEntity.Remove(0);
        if (IsDroppedItemInactive(droppedItem)) return true;

        error = "world-item-removal-failed";
        return false;
    }

    private static void ClearFixture(PickupFixture fixtureToClear)
    {
        if (fixtureToClear == null) return;
        if (ReferenceEquals(fixture, fixtureToClear))
            fixture = null;
        fixtureToClear.Agent = null;
        fixtureToClear.DroppedItem = null;
        fixtureToClear.OriginalWeapon = default;
        fixtureToClear.OriginalEquipment = default;
        fixtureToClear.ItemId = null;
        fixtureToClear.WorldItemId = Guid.Empty;
        fixtureToClear.SourceSlot = EquipmentIndex.None;
    }

    private static void EnsureCameraBehavior(Mission mission)
    {
        if (!ReferenceEquals(cameraBehavior, null) && ReferenceEquals(cameraBehavior.Mission, mission))
            return;
        cameraBehavior = new AgentCameraBehavior();
        mission.AddMissionBehavior(cameraBehavior);
    }

    private static bool UpdateCameraFrame()
    {
        if (ReferenceEquals(agentCamera, null) || ReferenceEquals(agentCamera.Entity, null) ||
            ReferenceEquals(focusedAgent, null) || !focusedAgent.IsActive())
        {
            return false;
        }

        GameEntity visualEntity = focusedAgent.AgentVisuals?.GetEntity();
        if (ReferenceEquals(visualEntity, null)) return false;
        MatrixFrame visualFrame = visualEntity.GetGlobalFrame();
        MatrixFrame localFrame = agentCameraLocalFrame;
        MatrixFrame globalFrame = visualFrame.TransformToParent(in localFrame);
        agentCamera.Entity.SetGlobalFrame(globalFrame);
        return true;
    }

    private static bool ReleaseCamera()
    {
        if (ReferenceEquals(agentCamera, null)) return false;
        if (ScreenManager.TopScreen is MissionScreen missionScreen &&
            ReferenceEquals(missionScreen.CustomCamera, agentCamera))
        {
            missionScreen.CustomCamera = null;
        }

        if (ReferenceEquals(agentCamera.Entity, null))
            agentCamera.ReleaseCamera();
        else
            agentCamera.ReleaseCameraEntity();
        agentCamera = null;
        focusedAgent = null;
        focusedAgentId = Guid.Empty;
        focusedView = "none";
        return true;
    }
}
#endif
