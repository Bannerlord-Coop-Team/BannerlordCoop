using ProtoBuf;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Packets
{
    [ProtoContract(SkipConstructor = true)]
    public readonly struct AgentEquipmentData
    {
        public AgentEquipmentData(Agent agent)
        {
            MainHandIndex = (int)agent.GetPrimaryWieldedItemIndex();
            OffHandIndex = (int)agent.GetOffhandWieldedItemIndex();
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
            if (mainHand != agent.GetPrimaryWieldedItemIndex() && CanWield(agent, mainHand))
                agent.SetWieldedItemIndexAsClient(Agent.HandIndex.MainHand, mainHand, false, false, agent.WieldedWeapon.CurrentUsageIndex);

            var offHand = (EquipmentIndex)OffHandIndex;
            if (offHand != agent.GetOffhandWieldedItemIndex() && CanWield(agent, offHand))
                agent.SetWieldedItemIndexAsClient(Agent.HandIndex.OffHand, offHand, false, false, agent.WieldedOffhandWeapon.CurrentUsageIndex);
        }

        // True when it is safe to wield this index on this agent: -1 (None) unwields (UpdateHumanStats guards the -1
        // case), and a weapon slot is only safe when it actually holds a weapon on this agent right now.
        private static bool CanWield(Agent agent, EquipmentIndex index)
        {
            if (index == EquipmentIndex.None) return true;
            return index >= EquipmentIndex.WeaponItemBeginSlot
                && index < EquipmentIndex.NumAllWeaponSlots
                && !agent.Equipment[index].IsEmpty;
        }

        internal static bool HasSafeWeaponSlots(MissionEquipment equipment)
        {
            return equipment?._weaponSlots != null &&
                   equipment._weaponSlots.Length >= (int)EquipmentIndex.NumAllWeaponSlots;
        }

        [ProtoMember(1)]
        public int MainHandIndex { get; }
        [ProtoMember(2)]
        public int OffHandIndex { get; }


    }
}
