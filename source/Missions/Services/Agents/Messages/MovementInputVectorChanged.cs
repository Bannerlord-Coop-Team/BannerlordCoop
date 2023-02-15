using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// An <see cref="IEvent"/> propagating that the <see cref="Agent"/>'s input vector has changed.
    /// </summary>
    public readonly struct MovementInputVectorChanged : IMovementEvent
    {
        /// <summary>
        /// The changed input vector.
        /// </summary>
        public Vec2 InputVector { get; }

        /// <inheritdoc />
        public Agent Agent { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="agent"></param>
        public MovementInputVectorChanged(Agent agent)
        {
            Agent = agent;
            InputVector = agent.MovementInputVector;
        }
    }
}
