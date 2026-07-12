using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Missions.Missiles.Message
{
    /// <summary>
    /// External event for agent missiles
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkAgentShoot : ICommand
    {
        [ProtoMember(1)]
        public Guid AgentId { get; }
        [ProtoMember(2)]
        public Vec3 Position { get; }
        [ProtoMember(3)]
        public Vec3 Velocity { get; }
        [ProtoMember(4)]
        public Mat3 Orientation { get; }
        [ProtoMember(5)]
        public bool HasRigidBody { get; }
        // ProtoMember(6) used to contain ItemObject, which protobuf-net cannot serialize. Keep that
        // tag retired and send the stable object id instead.
        [ProtoMember(13)]
        public string MissileItemId { get; }
        // ProtoMember(7) created a new, unregistered ItemModifier during deserialization. Native
        // missile construction requires the existing MBObjectManager instance, resolved by id.
        [ProtoMember(14)]
        public string ItemModifierId { get; }
        [ProtoMember(8)]
        public Banner Banner { get; }
        [ProtoMember(9)]
        public int MissileIndex { get; }
        [ProtoMember(10)]
        public float BaseSpeed { get; }
        [ProtoMember(11)]
        public float Speed { get; }
        // ProtoMember(12) was an incorrect "thrown weapon" flag. Native single-usage selection is
        // derived from WeaponsCount; the active usage index is the state that must cross the wire.
        [ProtoMember(15)]
        public int CurrentUsageIndex { get; }
        /// <summary>
        /// Per-shooter-session identity for this individual launch. Missile indices are native slots and can
        /// be reused; this sequence lets a later routed hit identify the exact reconstructed projectile.
        /// Zero is reserved for packets produced by older clients.
        /// </summary>
        [ProtoMember(16)]
        public long ShotSequence { get; }

        public NetworkAgentShoot(
            Guid agentId, 
            Vec3 position, 
            Vec3 velocity, 
            Mat3 orientation, 
            bool hasRigidBody, 
            string missileItemId,
            string itemModifierId,
            Banner banner, 
            int missileIndex, 
            float baseSpeed, 
            float speed,
            int currentUsageIndex,
            long shotSequence)
        {
            AgentId = agentId;
            Position = position;
            Velocity = velocity;
            Orientation = orientation;
            HasRigidBody = hasRigidBody;
            MissileItemId = missileItemId;
            ItemModifierId = itemModifierId;
            Banner = banner;
            MissileIndex = missileIndex;
            BaseSpeed = baseSpeed;
            Speed = speed;
            CurrentUsageIndex = currentUsageIndex;
            ShotSequence = shotSequence;
        }
    }
}
