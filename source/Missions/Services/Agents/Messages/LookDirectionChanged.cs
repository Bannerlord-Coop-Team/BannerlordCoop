using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// An <see cref="IEvent"/> propagating the <see cref="Agent"/>'s change in look direction.
    /// </summary>
    internal readonly struct LookDirectionChanged : IMovement
    {
        /// <summary>
        /// The changed vector representing the look direction.
        /// </summary>
        public Vec3 LookDirection { get; }

        /// <inheritdoc />
        public Agent Agent { get; }

        /// <inheritdoc />
        public Guid Guid { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="agent"></param>
        public LookDirectionChanged(Guid guid, Agent agent)
        {
            Agent = agent;
            Guid = guid;
            LookDirection = agent.LookDirection;
        }
    }
}
