using Common.Messaging;
using Missions.Services.Agents.Packets;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// An <see cref="IEvent"/> used to propagate movement related changes of an <see cref="Agent"/>.
    /// </summary>
    public abstract class Movement : IEvent
    {
        /// <summary>
        /// The <see cref="Agent"/> this <see cref="IEvent"/> is for.
        /// </summary>
        public Agent Agent { get; }

        /// <summary>
        /// The <paramref name="Agent"/>'s <see cref="Guid"/>.
        /// </summary>
        public Guid Guid { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="agent"></param>
        public Movement(Agent agent, Guid guid)
        {
            Agent = agent;
            Guid = guid;
        }

        /// <summary>
        /// Converts the <see cref="IEvent"/> to a <see cref="MovementPacket"/>.
        /// </summary>
        /// <returns></returns>
        public abstract MovementPacket ToMovementPacket();
    }
}
