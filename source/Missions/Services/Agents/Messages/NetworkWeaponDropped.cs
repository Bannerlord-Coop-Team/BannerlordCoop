using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public readonly struct NetworkWeaponDropped : INetworkEvent
    {
        [ProtoMember(1)]
        public Guid AgentGuid { get; }

        [ProtoMember(2)]
        public EquipmentIndex EquipmentIndex { get; }

        public NetworkWeaponDropped(Guid agentGuid, EquipmentIndex equipmentIndex)
        {
            AgentGuid = agentGuid;
            EquipmentIndex = equipmentIndex;
        }
    }
}