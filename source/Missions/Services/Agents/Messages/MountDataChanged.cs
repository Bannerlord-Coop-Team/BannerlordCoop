﻿using Missions.Services.Agents.Packets;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// An <see cref="IEvent"/> used to propagate movement related changes of <see cref="AgentMountData"/> of an <see cref="Agent"/>.
    /// </summary>
    public readonly struct MountDataChanged : IMovementEvent
    {
        /// <summary>
        /// The changed <see cref="AgentMountData"/> propagated by this <see cref="IEvent"/>.
        /// </summary>
        public AgentMountData AgentMountData { get; }

        /// <inheritdoc />
        public Agent Agent { get; }


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="agentMountData">optional</param>
        public MountDataChanged(Agent agent, AgentMountData agentMountData = null)
        {
            Agent = agent;
            AgentMountData = agentMountData ?? new AgentMountData(agent);
        }
    }
}
