using ProtoBuf;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Packets
{
    /// <summary>
    /// Struct representing the index of the main-hand and the off-hand.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public readonly struct AgentEquipmentData
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="agent"></param>
        public AgentEquipmentData(Agent agent)
        {
            MainHandIndex = (int)agent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
            OffHandIndex = (int)agent.GetWieldedItemIndex(Agent.HandIndex.OffHand);
        }

        /// <summary>
        /// Applies this <see cref="AgentEquipmentData"/> to the given <paramref name="agent"/>
        /// </summary>
        /// <param name="agent"></param>
        public void Apply(Agent agent)
        {
            // Check if there is a change on the right hand
            if ((EquipmentIndex)MainHandIndex != agent.GetWieldedItemIndex(Agent.HandIndex.MainHand))
            {
                // set the weapon to whatever index the server passed
                agent.SetWieldedItemIndexAsClient(Agent.HandIndex.MainHand, (EquipmentIndex)MainHandIndex, false, false, agent.WieldedWeapon.CurrentUsageIndex);
            }
            // check if there is a change on the left hand

            if ((EquipmentIndex)OffHandIndex != agent.GetWieldedItemIndex(Agent.HandIndex.OffHand))
            {
                // set the index to the weapon wielded
                agent.SetWieldedItemIndexAsClient(Agent.HandIndex.OffHand, (EquipmentIndex)OffHandIndex, false, false, agent.WieldedOffhandWeapon.CurrentUsageIndex);
            }
        }

        [ProtoMember(1)]
        public int MainHandIndex { get; }
        [ProtoMember(2)]
        public int OffHandIndex { get; }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj) || obj.GetType() != GetType()) return false;

            return this == (AgentEquipmentData)obj;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return MainHandIndex.GetHashCode() ^ OffHandIndex.GetHashCode();
        }

        // Remarks: Needs to be overriden or else the default-keyword does not work.
        public static bool operator ==(AgentEquipmentData a, AgentEquipmentData b)
        {
            return a.MainHandIndex == b.MainHandIndex && a.OffHandIndex == b.OffHandIndex;
        }

        public static bool operator !=(AgentEquipmentData a, AgentEquipmentData b)
        {
            return !(a == b);
        }
    }
}
