using Missions.Services.Agents.Packets;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// An <see cref="IEvent"/> used to propagate movement related changes of <see cref="AgentMountData"/> of an <see cref="Agent"/>.
    /// </summary>
    internal readonly struct MountDataChanged : IMovement
    {
        /// <summary>
        /// The changed <see cref="AgentMountData"/> propagated by this <see cref="IEvent"/>.
        /// </summary>
        public AgentMountData AgentMountData { get; }

        /// <inheritdoc />
        public Agent Agent { get; }

        /// <inheritdoc />
        public Guid Guid { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="agentMountData"></param>
        public MountDataChanged(Guid guid, Agent agent, AgentMountData agentMountData)
        {
            Agent = agent;
            Guid = guid;
            AgentMountData = agentMountData;
        }
    }
}
