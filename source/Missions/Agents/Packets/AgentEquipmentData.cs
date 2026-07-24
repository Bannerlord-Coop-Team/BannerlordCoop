using ProtoBuf;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Packets
{
    [ProtoContract(SkipConstructor = true)]
    public readonly struct AgentEquipmentData : IEquatable<AgentEquipmentData>
    {
        public AgentEquipmentData(Agent agent)
        {
            MainHandIndex = (int)agent.GetPrimaryWieldedItemIndex();
            OffHandIndex = (int)agent.GetOffhandWieldedItemIndex();
            MainHandUsageIndex = GetUsageIndex(agent.Equipment, (EquipmentIndex)MainHandIndex);
        }

        public void Apply(Agent agent)
        {
            // Bannerlord's wield-change callback always reads every weapon slot. During mission teardown and the
            // next tournament match's spawn, an agent can still be active while its MissionEquipment backing array
            // is temporarily incomplete. Do not invoke the native wield path until all weapon slots are available.
            if (!HasSafeWeaponSlots(agent?.Equipment)) return;

            // Only wield an index this agent actually has a weapon in RIGHT NOW. The sender's wielded index can point
            // to a slot that is EMPTY on this puppet — its loadout differs, its weapon depleted/broke, or this is a
            // stale packet landing as the mission tears down (the wielded weapon has already been put away). Wielding
            // an empty slot leaves a wielded index whose equipment[index].Item is null, and
            // SandboxAgentStatCalculateModel.UpdateHumanStats then dereferences item.WeaponComponent → NRE on every
            // following Formation.Tick (notably right after a battle / on host-migration adopt). Validating the slot
            // here covers both the wrong-index case and the end-of-battle race, since it reads the live equipment.
            var mainHand = (EquipmentIndex)MainHandIndex;
            int mainHandUsageIndex = GetSafeUsageIndex(agent.Equipment, mainHand, MainHandUsageIndex);
            if ((mainHand != agent.GetPrimaryWieldedItemIndex() ||
                 mainHandUsageIndex != GetUsageIndex(agent.Equipment, mainHand)) &&
                CanWield(agent, mainHand))
            {
                agent.SetWieldedItemIndexAsClient(
                    Agent.HandIndex.MainHand,
                    mainHand,
                    false,
                    false,
                    mainHandUsageIndex);
            }

            var offHand = (EquipmentIndex)OffHandIndex;
            // The native API's final argument is the main-hand usage index for both hand changes.
            if (offHand != agent.GetOffhandWieldedItemIndex() && CanWield(agent, offHand))
                agent.SetWieldedItemIndexAsClient(Agent.HandIndex.OffHand, offHand, false, false, mainHandUsageIndex);
        }

        // True when it is safe to wield this index on this agent: -1 (None) unwields (UpdateHumanStats guards the -1
        // case), and a weapon slot is only safe when it actually holds a weapon on this agent right now.
        private static bool CanWield(Agent agent, EquipmentIndex index)
        {
            if (index == EquipmentIndex.None) return true;
            return index >= EquipmentIndex.WeaponItemBeginSlot
                && index < EquipmentIndex.NumAllWeaponSlots
                && agent.Equipment[index].Item != null;
        }

        internal static bool HasSafeWeaponSlots(MissionEquipment equipment)
        {
            if (equipment?._weaponSlots == null ||
                equipment._weaponSlots.Length < (int)EquipmentIndex.NumAllWeaponSlots)
            {
                return false;
            }

            for (var index = EquipmentIndex.WeaponItemBeginSlot;
                 index < EquipmentIndex.NumAllWeaponSlots;
                 index++)
            {
                MissionWeapon weapon = equipment[index];
                if (weapon.Item != null &&
                    (weapon.CurrentUsageIndex < 0 || weapon.CurrentUsageIndex >= weapon.WeaponsCount))
                {
                    return false;
                }
            }

            return true;
        }

        private static int GetUsageIndex(MissionEquipment equipment, EquipmentIndex index)
        {
            if (index < EquipmentIndex.WeaponItemBeginSlot ||
                index >= EquipmentIndex.NumAllWeaponSlots ||
                equipment?._weaponSlots == null ||
                equipment._weaponSlots.Length <= (int)index)
            {
                return 0;
            }

            MissionWeapon weapon = equipment[index];
            return weapon.Item != null &&
                   weapon.CurrentUsageIndex >= 0 &&
                   weapon.CurrentUsageIndex < weapon.WeaponsCount
                ? weapon.CurrentUsageIndex
                : 0;
        }

        internal static int GetSafeUsageIndex(MissionEquipment equipment, EquipmentIndex index, int usageIndex)
        {
            if (index < EquipmentIndex.WeaponItemBeginSlot ||
                index >= EquipmentIndex.NumAllWeaponSlots ||
                equipment?._weaponSlots == null ||
                equipment._weaponSlots.Length <= (int)index)
            {
                return 0;
            }

            MissionWeapon weapon = equipment[index];
            return weapon.Item != null && usageIndex >= 0 && usageIndex < weapon.WeaponsCount
                ? usageIndex
                : 0;
        }

        public bool Equals(AgentEquipmentData other)
        {
            return MainHandIndex == other.MainHandIndex &&
                   OffHandIndex == other.OffHandIndex &&
                   MainHandUsageIndex == other.MainHandUsageIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is AgentEquipmentData other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = MainHandIndex;
                hashCode = (hashCode * 397) ^ OffHandIndex;
                return (hashCode * 397) ^ MainHandUsageIndex;
            }
        }

        [ProtoMember(1)]
        public int MainHandIndex { get; }
        [ProtoMember(2)]
        public int OffHandIndex { get; }
        [ProtoMember(3)]
        public int MainHandUsageIndex { get; }


    }
}
