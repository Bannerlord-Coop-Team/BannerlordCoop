using Missions.Services.Agents.Packets;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// An <see cref="IEvent"/> used to propagate movement related changes of <see cref="AgentMountData"/> of an <see cref="Agent"/>.
    /// </summary>
    public sealed class MountDataChanged : Movement
    {
        /// <summary>
        /// The changed <see cref="AgentMountData"/> propagated by this <see cref="IEvent"/>.
        /// </summary>
        public AgentMountData AgentMountData { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="agentMountData"></param>
        public MountDataChanged(Agent agent, AgentMountData agentMountData, Guid guid) : base(agent, guid)
        {
            AgentMountData = agentMountData;
        }

        /// <inheritdoc />
        public override MovementPacket ToMovementPacket()
        {
            return new MovementPacket(Guid, new AgentData(Agent, AgentMountData));
        }
    }
}
