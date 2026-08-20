using Common.Messaging;
using Missions.Agents.Packets;
using ProtoBuf;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Missions.Agents.Messages
{
    /// <summary>
    /// External event for agent weapon drops
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public sealed class NetworkWeaponDropped : IEvent
    {
        [ProtoMember(1)]
        public Guid AgentId { get; }

        [ProtoMember(2)]
        public EquipmentIndex EquipmentIndex { get; }

        [ProtoMember(3)]
        public Guid WorldItemId { get; }

        [ProtoMember(4)]
        public Guid DropId { get; }

        [ProtoMember(5)]
        public string OriginControllerId { get; }

        [ProtoMember(6)]
        public string ItemObjectId { get; }

        [ProtoMember(7)]
        public string ItemModifierId { get; }

        [ProtoMember(8)]
        public string BannerCode { get; }

        [ProtoMember(9)]
        public short DataValue { get; }

        [ProtoMember(10)]
        public Vec3 Position { get; }

        [ProtoMember(11)]
        public Mat3 Rotation { get; }

        [ProtoMember(12)]
        public int SpawnFlags { get; }

        [ProtoMember(13)]
        public bool HasLifeTime { get; }

        [ProtoMember(14)]
        public AgentEquipmentData CurrentEquipment { get; }

        [ProtoMember(15)]
        public bool HasCurrentEquipment { get; }

        [ProtoMember(16)]
        public bool IsCatchUp { get; }

        [ProtoMember(17)]
        public float RemainingLifeTime { get; }

        public NetworkWeaponDropped(
            Guid dropId,
            Guid agentId,
            EquipmentIndex equipmentIndex,
            Guid worldItemId,
            string originControllerId,
            string itemObjectId,
            string itemModifierId,
            string bannerCode,
            short dataValue,
            Vec3 position,
            Mat3 rotation,
            int spawnFlags,
            bool hasLifeTime,
            float remainingLifeTime,
            AgentEquipmentData? currentEquipment,
            bool isCatchUp)
        {
            DropId = dropId;
            AgentId = agentId;
            EquipmentIndex = equipmentIndex;
            WorldItemId = worldItemId;
            OriginControllerId = originControllerId;
            ItemObjectId = itemObjectId;
            ItemModifierId = itemModifierId;
            BannerCode = bannerCode;
            DataValue = dataValue;
            Position = position;
            Rotation = rotation;
            SpawnFlags = spawnFlags;
            HasLifeTime = hasLifeTime;
            RemainingLifeTime = remainingLifeTime;
            CurrentEquipment = currentEquipment.GetValueOrDefault();
            HasCurrentEquipment = currentEquipment.HasValue;
            IsCatchUp = isCatchUp;
        }
    }
}
