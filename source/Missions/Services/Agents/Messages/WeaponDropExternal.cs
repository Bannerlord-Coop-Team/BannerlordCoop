using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public readonly struct WeaponDropExternal : INetworkEvent
    {
        [ProtoMember(1)]
        public Guid AgentGuid { get; }

        [ProtoMember(2)]
        public EquipmentIndex EquipmentIndex { get; }

        public WeaponDropExternal(Guid agentGuid, EquipmentIndex equipmentIndex)
        {
            AgentGuid = agentGuid;
            EquipmentIndex = equipmentIndex;
        }
    }
}