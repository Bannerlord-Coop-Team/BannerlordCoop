using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    [ProtoContract]
    public readonly struct AgentShoot : INetworkEvent
    {
        [ProtoMember(1)]
        public Agent Agent { get; }
        [ProtoMember(2)]
        public EquipmentIndex WeaponIndex { get; }
        [ProtoMember(3)]
        public Vec3 Position { get; }
        [ProtoMember(4)]
        public Vec3 Velocity { get; }
        [ProtoMember(5)]
        public Mat3 Orientation { get; }
        [ProtoMember(6)]
        public bool HasRigidBody { get; }
        [ProtoMember(7)]
        public int ForcedMissileIndex { get; }

        public AgentShoot(Agent agent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
        {
            Agent = agent;
            WeaponIndex = weaponIndex;
            Position = position;
            Velocity = velocity;
            Orientation = orientation;
            HasRigidBody = hasRigidBody;
            ForcedMissileIndex = forcedMissileIndex;
        }
    }
}
