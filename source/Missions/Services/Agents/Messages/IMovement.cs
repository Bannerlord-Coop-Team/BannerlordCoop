using Common.Messaging;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// An <see cref="IEvent"/> used to propagate movement related changes of an <see cref="Agent"/>.
    /// </summary>
    interface IMovement : IEvent
    {
        /// <summary>
        /// The <see cref="Agent"/> this <see cref="IEvent"/> is for.
        /// </summary>
        Agent Agent { get; }

        /// <summary>
        /// The <see cref="Agents"/>'s network <see cref="Guid"/>.
        /// </summary>
        Guid Guid { get; }
    }
}
