using ProtoBuf;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Packets
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
            // Check if there is a change on the right hand
            if ((EquipmentIndex)MainHandIndex != agent.GetPrimaryWieldedItemIndex())
            {
                // set the weapon to whatever index the server passed
                agent.SetWieldedItemIndexAsClient(Agent.HandIndex.MainHand, (EquipmentIndex)MainHandIndex, false, false, agent.WieldedWeapon.CurrentUsageIndex);
            }
            // check if there is a change on the left hand

            if ((EquipmentIndex)OffHandIndex != agent.GetOffhandWieldedItemIndex())
            {
                // set the index to the weapon wielded
                agent.SetWieldedItemIndexAsClient(Agent.HandIndex.OffHand, (EquipmentIndex)OffHandIndex, false, false, agent.WieldedOffhandWeapon.CurrentUsageIndex);
            }
        }

        [ProtoMember(1)]
        public int MainHandIndex { get; }
        [ProtoMember(2)]
        public int OffHandIndex { get; }


    }
}
