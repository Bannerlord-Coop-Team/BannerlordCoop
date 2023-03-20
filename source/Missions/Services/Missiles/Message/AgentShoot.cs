using Common.Messaging;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Missiles.Message
{
    public readonly struct AgentShoot : IEvent
    {
        public Agent Agent { get; }
        public Vec3 Position { get; }
        public Vec3 Direction { get; }
        public Mat3 Orientation { get; }
        public bool HasRigidBody { get; }
        public int ForcedMissileIndex { get; }
        public int MissileIndex { get; }

        public AgentShoot(Agent agent, Vec3 position, Vec3 direction, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex, int missileIndex)
        {
            Agent = agent;
            Position = position;
            Direction = direction;
            Orientation = orientation;
            HasRigidBody = hasRigidBody;
            ForcedMissileIndex = forcedMissileIndex;
            MissileIndex = missileIndex;
        }
    }
}
