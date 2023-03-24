using Common.Messaging;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Missiles.Message
{
    public readonly struct AgentShoot : IEvent
    {
        public Agent Agent { get; }
        public MissionWeapon MissionWeapon { get; }
        public Vec3 Position { get; }
        public Vec3 Direction { get; }
        public Mat3 Orientation { get; }
        public float BaseSpeed { get; }
        public float Speed { get; }
        public bool HasRigidBody { get; }
        public int MissileIndex { get; }

        public AgentShoot(Agent agent, MissionWeapon missionWeapon, Vec3 position, Vec3 direction, Mat3 orientation, float baseSpeed, float speed, bool hasRigidBody, int missileIndex)
        {
            Agent = agent;
            MissionWeapon = missionWeapon;
            Position = position;
            Direction = direction;
            Orientation = orientation;
            BaseSpeed = baseSpeed;
            Speed = speed;
            HasRigidBody = hasRigidBody;
            MissileIndex = missileIndex;
        }
    }
}
