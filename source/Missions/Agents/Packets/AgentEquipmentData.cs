using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Packets
{
    [ProtoContract(SkipConstructor = true)]
    public readonly struct AgentEquipmentData : IEquatable<AgentEquipmentData>
    {
        public AgentEquipmentData(Agent agent)
        {
            if (!TryRead(agent, out EquipmentIndex mainHandIndex,
                    out EquipmentIndex offHandIndex, out int mainHandUsageIndex))
            {
                mainHandIndex = EquipmentIndex.None;
                offHandIndex = EquipmentIndex.None;
                mainHandUsageIndex = 0;
            }

            MainHandIndex = (int)mainHandIndex;
            OffHandIndex = (int)offHandIndex;
            MainHandUsageIndex = mainHandUsageIndex;
            MainHandItemId = GetItemId(agent?.Equipment, mainHandIndex);
            OffHandItemId = GetItemId(agent?.Equipment, offHandIndex);
            ArrowAmmo = agent?.IsHuman == true ? CaptureArrowAmmo(agent.Equipment) : null;
        }

        internal static bool TryCapture(Agent agent, out AgentEquipmentData data)
        {
            data = default;
            if (!TryRead(agent, out EquipmentIndex mainHandIndex,
                    out EquipmentIndex offHandIndex, out int mainHandUsageIndex))
            {
                return false;
            }

            data = new AgentEquipmentData(
                mainHandIndex, offHandIndex, mainHandUsageIndex,
                GetItemId(agent.Equipment, mainHandIndex),
                GetItemId(agent.Equipment, offHandIndex), CaptureArrowAmmo(agent.Equipment));
            return true;
        }

        private static bool TryRead(
            Agent agent,
            out EquipmentIndex mainHandIndex,
            out EquipmentIndex offHandIndex,
            out int mainHandUsageIndex)
        {
            mainHandIndex = EquipmentIndex.None;
            offHandIndex = EquipmentIndex.None;
            mainHandUsageIndex = 0;
            if (agent == null || !agent.IsHuman)
                return false;

            mainHandIndex = agent.GetPrimaryWieldedItemIndex();
            offHandIndex = agent.GetOffhandWieldedItemIndex();
            mainHandUsageIndex = GetUsageIndex(agent.Equipment, mainHandIndex);
            return true;
        }

        internal AgentEquipmentData(
            EquipmentIndex mainHandIndex,
            EquipmentIndex offHandIndex,
            int mainHandUsageIndex,
            string mainHandItemId = null,
            string offHandItemId = null,
            AgentArrowAmmoData[] arrowAmmo = null)
        {
            MainHandIndex = (int)mainHandIndex;
            OffHandIndex = (int)offHandIndex;
            MainHandUsageIndex = mainHandUsageIndex;
            MainHandItemId = mainHandItemId;
            OffHandItemId = offHandItemId;
            ArrowAmmo = arrowAmmo;
        }

        internal bool TryApplyForAction(Agent agent)
        {
            if (agent?.IsHuman != true || !HasSafeWeaponSlots(agent.Equipment)) return false;
            var mainHand = (EquipmentIndex)MainHandIndex;
            var offHand = (EquipmentIndex)OffHandIndex;
            if (!CanWield(agent, mainHand) || !CanWield(agent, offHand)
                || MainHandUsageIndex != GetSafeUsageIndex(agent.Equipment, mainHand, MainHandUsageIndex)
                || !MatchesItem(agent.Equipment, mainHand, MainHandItemId)
                || !MatchesItem(agent.Equipment, offHand, OffHandItemId)
                || !CanApplyArrowAmmo(agent.Equipment))
            {
                return false;
            }

            Apply(agent);
            if (ArrowAmmo != null)
            {
                foreach (var ammo in ArrowAmmo)
                {
                    if (agent.Equipment[(EquipmentIndex)ammo.Slot].Amount != ammo.Amount)
                        agent.SetWeaponAmountInSlot((EquipmentIndex)ammo.Slot, ammo.Amount, enforcePrimaryItem: true);
                }
            }
            return Matches(agent);
        }

        internal bool Matches(Agent agent)
        {
            return TryCapture(agent, out AgentEquipmentData current)
                && MainHandIndex == current.MainHandIndex
                && OffHandIndex == current.OffHandIndex
                && MainHandUsageIndex == current.MainHandUsageIndex
                && (MainHandItemId == null || MainHandItemId == current.MainHandItemId)
                && (OffHandItemId == null || OffHandItemId == current.OffHandItemId)
                && (ArrowAmmo == null || ArrowAmmo.SequenceEqual(current.ArrowAmmo ?? Array.Empty<AgentArrowAmmoData>()));
        }

        private static AgentArrowAmmoData[] CaptureArrowAmmo(MissionEquipment equipment)
        {
            if (!HasSafeWeaponSlots(equipment)) return null;
            List<AgentArrowAmmoData> amounts = null;
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.ExtraWeaponSlot; slot++)
            {
                var weapon = equipment[slot];
                if (!IsArrowAmmo(weapon)) continue;
                (amounts ??= new List<AgentArrowAmmoData>()).Add(new AgentArrowAmmoData(
                    (int)slot, weapon.Item.StringId, weapon.ItemModifier?.StringId, weapon.Amount));
            }
            return amounts?.ToArray();
        }

        private static bool IsArrowAmmo(MissionWeapon weapon)
        {
            var weaponClass = weapon.CurrentUsageItem?.WeaponClass;
            return weapon.Item != null && (weaponClass == WeaponClass.Arrow || weaponClass == WeaponClass.Bolt);
        }

        private bool CanApplyArrowAmmo(MissionEquipment equipment)
        {
            if (ArrowAmmo == null) return true;
            if (ArrowAmmo.Length > (int)EquipmentIndex.ExtraWeaponSlot) return false;
            int expectedCount = 0;
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.ExtraWeaponSlot; slot++)
                if (IsArrowAmmo(equipment[slot])) expectedCount++;
            if (ArrowAmmo.Length != expectedCount) return false;
            int previousSlot = -1;
            foreach (var ammo in ArrowAmmo)
            {
                if (ammo.Slot <= previousSlot || ammo.Slot < (int)EquipmentIndex.WeaponItemBeginSlot ||
                    ammo.Slot >= (int)EquipmentIndex.ExtraWeaponSlot || string.IsNullOrEmpty(ammo.ItemId)) return false;
                previousSlot = ammo.Slot;
                var weapon = equipment[(EquipmentIndex)ammo.Slot];
                if (!IsArrowAmmo(weapon) || weapon.Item.StringId != ammo.ItemId ||
                    weapon.ItemModifier?.StringId != ammo.ItemModifierId ||
                    ammo.Amount < 0 || ammo.Amount > weapon.ModifiedMaxAmount) return false;
            }
            return true;
        }

        private static bool MatchesItem(MissionEquipment equipment, EquipmentIndex index, string itemId)
        {
            return itemId == null || itemId == GetItemId(equipment, index);
        }

        private static string GetItemId(MissionEquipment equipment, EquipmentIndex index)
        {
            if (index < EquipmentIndex.WeaponItemBeginSlot || index >= EquipmentIndex.NumAllWeaponSlots
                || equipment?._weaponSlots == null || equipment._weaponSlots.Length <= (int)index)
            {
                return string.Empty;
            }
            return equipment[index].Item?.StringId ?? string.Empty;
        }

        public void Apply(Agent agent)
        {
            // Bannerlord's wield-change callback always reads every weapon slot. During mission teardown and the
            // next tournament match's spawn, an agent can still be active while its MissionEquipment backing array
            // is temporarily incomplete. Do not invoke the native wield path until all weapon slots are available.
            if (agent?.IsHuman != true || !HasSafeWeaponSlots(agent.Equipment)) return;

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
                   MainHandUsageIndex == other.MainHandUsageIndex
                   && MainHandItemId == other.MainHandItemId
                   && OffHandItemId == other.OffHandItemId
                   && (ArrowAmmo ?? Array.Empty<AgentArrowAmmoData>()).SequenceEqual(
                       other.ArrowAmmo ?? Array.Empty<AgentArrowAmmoData>());
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
                hashCode = (hashCode * 397) ^ MainHandUsageIndex;
                hashCode = (hashCode * 397) ^ (MainHandItemId?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (OffHandItemId?.GetHashCode() ?? 0);
                if (ArrowAmmo != null)
                    foreach (var ammo in ArrowAmmo) hashCode = (hashCode * 397) ^ ammo.GetHashCode();
                return hashCode;
            }
        }

        [ProtoMember(1)]
        public int MainHandIndex { get; }
        [ProtoMember(2)]
        public int OffHandIndex { get; }
        [ProtoMember(3)]
        public int MainHandUsageIndex { get; }
        [ProtoMember(4)]
        public string MainHandItemId { get; }
        [ProtoMember(5)]
        public string OffHandItemId { get; }
        [ProtoMember(6)]
        public AgentArrowAmmoData[] ArrowAmmo { get; }
    }

    [ProtoContract(SkipConstructor = true)]
    public readonly struct AgentArrowAmmoData
    {
        public AgentArrowAmmoData(int slot, string itemId, string itemModifierId, short amount)
        {
            Slot = slot;
            ItemId = itemId;
            ItemModifierId = itemModifierId;
            Amount = amount;
        }

        [ProtoMember(1)]
        public int Slot { get; }
        [ProtoMember(2)]
        public string ItemId { get; }
        [ProtoMember(3)]
        public string ItemModifierId { get; }
        [ProtoMember(4)]
        public short Amount { get; }
    }
}
