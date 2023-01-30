using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// An <see cref="IEvent"/> propagating that the <see cref="Agent"/>'s input vector has changed.
    /// </summary>
    internal readonly struct MovementInputVectorChanged : IMovement
    {
        /// <summary>
        /// The changed input vector.
        /// </summary>
        public Vec2 InputVector { get; }

        /// <inheritdoc />
        public Agent Agent { get; }

        /// <inheritdoc />
        public Guid Guid { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="agent"></param>
        public MovementInputVectorChanged(Guid guid, Agent agent)
        {
            Agent = agent;
            Guid = guid;
            InputVector = agent.MovementInputVector;
        }
    }
}
