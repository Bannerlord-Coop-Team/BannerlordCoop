using Missions.Services.Agents.Packets;
using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// An <see cref="IEvent"/> propagating the <see cref="Agent"/>'s change in look direction.
    /// </summary>
    public sealed class LookDirectionChanged : Movement
    {
        /// <summary>
        /// The changed vector representing the look direction.
        /// </summary>
        public Vec3 LookDirection { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="agent"></param>
        public LookDirectionChanged(Agent agent, Guid guid) : base(agent, guid)
        {
            LookDirection = agent.LookDirection;
        }

        /// <inheritdoc />
        public override MovementPacket ToMovementPacket()
        {
            return new MovementPacket(Guid, new AgentData(Agent, LookDirection));
        }
    }
}
