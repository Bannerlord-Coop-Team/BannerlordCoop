using Common.Messaging;
using Common.Util;
using HarmonyLib;
using Missions.Agents.Messages;
using System.Collections.Generic;
using TaleWorlds.Core;
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
        internal readonly struct DropState
        {
            public HashSet<SpawnedItemEntity> ExistingItems { get; }
            public MissionWeapon Weapon { get; }

            public DropState(
                HashSet<SpawnedItemEntity> existingItems,
                MissionWeapon weapon)
            {
                ExistingItems = existingItems;
                Weapon = weapon;
            }
        }

        // DropItem spawns its world item through the native drop callback, which announces the drop itself.
        [System.ThreadStatic]
        private static bool insideDropItem;

        internal static bool IsInsideDropItem => insideDropItem;

        static void Prefix(
            EquipmentIndex itemIndex,
            Agent __instance,
            out DropState __state)
        {
            insideDropItem = true;
            __state = Capture(__instance, itemIndex);
        }

        static void Postfix(
            EquipmentIndex itemIndex,
            Agent __instance,
            DropState __state)
        {
            Publish(__instance, itemIndex, __state);
        }

        static void Finalizer()
        {
            insideDropItem = false;
        }

        internal static DropState Capture(Agent agent, EquipmentIndex itemIndex)
        {
            if (AllowedThread.IsThisThreadAllowed()) return default;

            MissionWeapon weapon = default;
            if (agent != null &&
                itemIndex >= EquipmentIndex.WeaponItemBeginSlot &&
                itemIndex < EquipmentIndex.NumAllWeaponSlots)
            {
                weapon = agent.Equipment[itemIndex];
            }

            return new DropState(WeaponDropItemTracker.Capture(), weapon);
        }

        internal static void Publish(Agent agent, EquipmentIndex itemIndex, DropState state)
        {
            if (AllowedThread.IsThisThreadAllowed() || state.ExistingItems == null) return;

            SpawnedItemEntity droppedItem = WeaponDropItemTracker.FindDroppedItem(state.ExistingItems);
            WeaponDropped message = new WeaponDropped(
                agent,
                itemIndex,
                state.Weapon,
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
        static void Prefix(
            Agent agent,
            EquipmentIndex equipmentIndex,
            out AgentDropPatch.DropState __state)
        {
            __state = AgentDropPatch.IsInsideDropItem
                ? default
                : AgentDropPatch.Capture(agent, equipmentIndex);
        }

        static void Postfix(
            Agent agent,
            EquipmentIndex equipmentIndex,
            AgentDropPatch.DropState __state)
        {
            AgentDropPatch.Publish(agent, equipmentIndex, __state);
        }
    }
}
