using Missions.Services.Agents.Packets;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// An <see cref="IEvent"/> used to propagate movement related changes of <see cref="AgentActionData"/> of an <see cref="Agent"/>.
    /// </summary>
    public sealed class ActionDataChanged : Movement
    {
        /// <summary>
        /// The changed <see cref="AgentActionData"/> propagated by this <see cref="IEvent"/>.
        /// </summary>
        public AgentActionData AgentActionData { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="agentActionData"></param>
        public ActionDataChanged(Agent agent, AgentActionData agentActionData, Guid guid) : base(agent, guid)
        {
            AgentActionData = agentActionData;
        }

        /// <inheritdoc />
        public override MovementPacket ToMovementPacket()
        {
            return new MovementPacket(Guid, new AgentData(Agent, AgentActionData));
        }
    }
}
