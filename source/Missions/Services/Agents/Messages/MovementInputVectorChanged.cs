using Missions.Services.Agents.Packets;
using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// An <see cref="IEvent"/> propagating that the <see cref="Agent"/>'s input vector has changed.
    /// </summary>
    public sealed class MovementInputVectorChanged : Movement
    {
        /// <summary>
        /// The changed input vector.
        /// </summary>
        public Vec2 InputVector { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="agent"></param>
        public MovementInputVectorChanged(Agent agent, Guid guid) : base(agent, guid)
        {
            InputVector = agent.MovementInputVector;
        }

        /// <inheritdoc />
        public override MovementPacket ToMovementPacket()
        {
            return new MovementPacket(Guid, new AgentData(Agent, InputVector));
        }
    }
}
