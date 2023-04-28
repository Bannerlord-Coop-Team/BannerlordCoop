using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// An <see cref="IEvent"/> propagating the <see cref="Agent"/>'s change in look direction.
    /// </summary>
    public readonly struct LookDirectionChanged : IMovementEvent
    {
        /// <summary>
        /// The changed vector representing the look direction.
        /// </summary>
        public Vec3 LookDirection { get; }

        /// <inheritdoc />
        public Agent Agent { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="agent"></param>
        public LookDirectionChanged(Agent agent)
        {
            Agent = agent;
            LookDirection = agent.LookDirection;
        }
    }
}
