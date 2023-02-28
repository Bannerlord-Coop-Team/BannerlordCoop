using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    [ProtoContract]
    public readonly struct WeaponDropExternal : INetworkEvent
    {
        [ProtoMember(1)]
        public Guid AgentGuid { get; }

        [ProtoMember(2)]
        public EquipmentIndex EquipmentIndex { get; }

        [ProtoMember(3)]
        public WeaponClass WeaponClass { get; }

        public WeaponDropExternal(Guid agentGuid, EquipmentIndex equipmentIndex, WeaponClass weaponClass)
        {
            AgentGuid = agentGuid;
            EquipmentIndex = equipmentIndex;
            WeaponClass = weaponClass;
        }
    }
}