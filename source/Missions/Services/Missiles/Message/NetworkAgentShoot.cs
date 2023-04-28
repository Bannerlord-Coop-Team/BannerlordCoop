using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Missions.Services.Missiles.Message
{
    /// <summary>
    /// External event for agent missiles
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkAgentShoot : INetworkEvent
    {
        [ProtoMember(1)]
        public Guid AgentGuid { get; }
        [ProtoMember(2)]
        public Vec3 Position { get; }
        [ProtoMember(3)]
        public Vec3 Velocity { get; }
        [ProtoMember(4)]
        public Mat3 Orientation { get; }
        [ProtoMember(5)]
        public bool HasRigidBody { get; }
        [ProtoMember(6)]
        public ItemObject ItemObject { get; }
        [ProtoMember(7)]
        public ItemModifier ItemModifier { get; }
        [ProtoMember(8)]
        public Banner Banner { get; }
        [ProtoMember(9)]
        public int MissileIndex { get; }
        [ProtoMember(10)]
        public float BaseSpeed { get; }
        [ProtoMember(11)]
        public float Speed { get; }
        [ProtoMember(12)]
        public bool SingleUse { get; }

        public NetworkAgentShoot(
            Guid agentGuid, 
            Vec3 position, 
            Vec3 velocity, 
            Mat3 orientation, 
            bool hasRigidBody, 
            ItemObject itemObject, 
            ItemModifier itemModifier, 
            Banner banner, 
            int missileIndex, 
            float baseSpeed, 
            float speed,
            bool singleUse)
        {
            AgentGuid = agentGuid;
            Position = position;
            Velocity = velocity;
            Orientation = orientation;
            HasRigidBody = hasRigidBody;
            ItemObject = itemObject;
            ItemModifier = itemModifier;
            Banner = banner;
            MissileIndex = missileIndex;
            BaseSpeed = baseSpeed;
            Speed = speed;
            SingleUse = singleUse;
        }
    }
}