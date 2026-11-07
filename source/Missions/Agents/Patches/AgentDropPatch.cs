using Common.Messaging;
using Common.Util;
using GameInterface;
using HarmonyLib;
using Missions.Agents.Messages;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches
{
    /// <summary>
    /// Patch on DropItem for WeaponDropHandler
    /// </summary>
    [HarmonyPatch(typeof(Agent), "DropItem")]
    [HarmonyPatchCategory(MissionModule.WeaponDropPatchCategory)]
    public class AgentDropPatch
    {
        /// <summary>The weapon a registered agent is about to drop, captured before the slot empties.</summary>
        internal readonly struct DropState
        {
            public bool IsCaptured { get; }
            public MissionWeapon Weapon { get; }

            public DropState(MissionWeapon weapon)
            {
                IsCaptured = true;
                Weapon = weapon;
            }
        }

        // DropItem spawns its world item through the native drop callback, which hands the item back here.
        static void Prefix(
            EquipmentIndex itemIndex,
            Agent __instance,
            out DropState __state)
        {
            __state = Capture(__instance, itemIndex);
            if (__state.IsCaptured) WeaponDropItemTracker.Begin();
        }

        // Announces the drop with the world item the native callback reported.
        static void Postfix(
            EquipmentIndex itemIndex,
            Agent __instance,
            DropState __state)
        {
            if (!__state.IsCaptured) return;

            Publish(__instance, itemIndex, __state.Weapon, WeaponDropItemTracker.End());
        }

        // Stops waiting for the world item even when DropItem throws.
        static void Finalizer(DropState __state)
        {
            if (__state.IsCaptured) WeaponDropItemTracker.End();
        }

        // Captures the dropped slot's weapon for a registered agent; anything else is never announced.
        internal static DropState Capture(Agent agent, EquipmentIndex itemIndex)
        {
            if (agent == null || AllowedThread.IsThisThreadAllowed()) return default;
            if (!ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry) ||
                !registry.TryGetAgentInfo(agent, out _))
            {
                return default;
            }

            MissionWeapon weapon = default;
            if (itemIndex >= EquipmentIndex.WeaponItemBeginSlot &&
                itemIndex < EquipmentIndex.NumAllWeaponSlots)
            {
                weapon = agent.Equipment[itemIndex];
            }

            return new DropState(weapon);
        }

        // Announces a captured drop together with the world item it spawned, if any.
        internal static void Publish(
            Agent agent,
            EquipmentIndex itemIndex,
            MissionWeapon weapon,
            SpawnedItemEntity droppedItem)
        {
            WeaponDropped message = new WeaponDropped(
                agent,
                itemIndex,
                weapon,
                droppedItem);
            MessageBroker.Instance.Publish(agent, message);
        }
    }

    /// <summary>
    /// Native death drops reach the engine's drop callback directly instead of going through
    /// <see cref="Agent.DropItem"/>, so their world items would otherwise never receive the owner's canonical
    /// identity. Patched on the Aux body the one-line callback forwards to, which the JIT cannot inline away.
    /// </summary>
    [HarmonyPatch(typeof(Mission), nameof(Mission.SpawnWeaponAsDropFromAgentAux))]
    [HarmonyPatchCategory(MissionModule.WeaponDropPatchCategory)]
    public class NativeAgentDropPatch
    {
        /// <summary>What the native drop callback needs to hand its spawned world item to the right owner.</summary>
        internal readonly struct NativeDropState
        {
            public bool RecordsForCaller { get; }
            public AgentDropPatch.DropState Drop { get; }
            public WeakGameEntity WeaponEntity { get; }

            public NativeDropState(
                bool recordsForCaller,
                AgentDropPatch.DropState drop,
                WeakGameEntity weaponEntity)
            {
                RecordsForCaller = recordsForCaller;
                Drop = drop;
                WeaponEntity = weaponEntity;
            }
        }

        // Captures the weapon entity before the callback turns it into a world item.
        static void Prefix(
            Agent agent,
            EquipmentIndex equipmentIndex,
            out NativeDropState __state)
        {
            __state = Capture(agent, equipmentIndex);
        }

        // Reads the world item off the captured entity instead of scanning mission objects.
        static void Postfix(
            Agent agent,
            EquipmentIndex equipmentIndex,
            NativeDropState __state)
        {
            Publish(agent, equipmentIndex, __state);
        }

        // Captures the slot's weapon entity, which the callback turns into the world item, only when someone needs it.
        internal static NativeDropState Capture(Agent agent, EquipmentIndex equipmentIndex)
        {
            bool recordsForCaller = WeaponDropItemTracker.IsCapturing;
            AgentDropPatch.DropState drop = recordsForCaller
                ? default
                : AgentDropPatch.Capture(agent, equipmentIndex);
            if (agent == null || (!recordsForCaller && !drop.IsCaptured)) return default;

            return new NativeDropState(
                recordsForCaller,
                drop,
                agent.GetWeaponEntityFromEquipmentSlot(equipmentIndex));
        }

        // Hands the spawned world item to the caller waiting on it, or announces the death drop itself.
        internal static void Publish(Agent agent, EquipmentIndex equipmentIndex, NativeDropState state)
        {
            if (!state.RecordsForCaller && !state.Drop.IsCaptured) return;

            SpawnedItemEntity droppedItem = WeaponDropItemTracker.FindDroppedItem(state.WeaponEntity);
            if (state.RecordsForCaller)
            {
                WeaponDropItemTracker.Record(droppedItem);
                return;
            }

            AgentDropPatch.Publish(agent, equipmentIndex, state.Drop.Weapon, droppedItem);
        }
    }
}
