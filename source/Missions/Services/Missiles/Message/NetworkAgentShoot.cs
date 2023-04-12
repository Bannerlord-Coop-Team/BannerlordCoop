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
        public Vec3 Orientations{ get; }
        [ProtoMember(5)]
        public Vec3 Orientationf { get; }
        [ProtoMember(6)]
        public Vec3 Orientationu { get; }
        [ProtoMember(7)]
        public bool HasRigidBody { get; }
        [ProtoMember(8)]
        public ItemObject ItemObject { get; }
        [ProtoMember(9)]
        public ItemModifier ItemModifier { get; }
        [ProtoMember(10)]
        public Banner Banner { get; }
        [ProtoMember(11)]
        public int MissileIndex { get; }

        [ProtoMember(12)]
        public float BaseSpeed { get; }
        [ProtoMember(13)]
        public float Speed { get; }

        public NetworkAgentShoot(Guid agentGuid, Vec3 position, Vec3 velocity, Vec3 orientationS, Vec3 orientationF, Vec3 orientationU, bool hasRigidBody, ItemObject itemObject, ItemModifier itemModifier, Banner banner, int missileIndex, float baseSpeed, float speed)
        {
            AgentGuid = agentGuid;
            Position = position;
            Velocity = velocity;
            Orientations = orientationS;
            Orientationf = orientationF;
            Orientationu = orientationU;
            HasRigidBody = hasRigidBody;
            ItemObject = itemObject;
            ItemModifier = itemModifier;
            Banner = banner;
            MissileIndex = missileIndex;
            BaseSpeed = baseSpeed;
            Speed = speed;
        }
    }
}