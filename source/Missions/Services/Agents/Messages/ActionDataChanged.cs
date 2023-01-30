using Missions.Services.Agents.Packets;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// An <see cref="IEvent"/> used to propagate movement related changes of <see cref="AgentActionData"/> of an <see cref="Agent"/>.
    /// </summary>
    internal readonly struct ActionDataChanged : IMovement
    {
        /// <summary>
        /// The changed <see cref="AgentActionData"/> propagated by this <see cref="IEvent"/>.
        /// </summary>
        public AgentActionData AgentActionData { get; }

        /// <inheritdoc />
        public Agent Agent { get; }

        /// <inheritdoc />
        public Guid Guid { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="agentActionData"></param>
        public ActionDataChanged(Guid guid, Agent agent, AgentActionData agentActionData)
        {
            Agent = agent;
            Guid = guid;
            AgentActionData = agentActionData;
        }

    }
}
