using Common.Logging;
using Common.Messaging;
using Missions.Services.Agents.Messages;
using Missions.Services.Agents.Packets;
using Missions.Services.Network;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents
{
    /// <summary>
    /// Manages player controlled <see cref="AgentMovement"/>.
    /// </summary>
    public class AgentPublisher
    {
        private Guid _agentId;

        private AgentMovement _agentMovement;

        private static readonly ILogger _logger = LogManager.GetLogger<AgentPublisher>();

        private IMessageBroker _messageBroker;

        private readonly IAgentPublisherConfig _agentPublisherConfig;

        private readonly INetworkAgentRegistry _networkAgentRegistry;

        private Timer _pollAndUpdateAgentMovementTimer;

        /// <summary>
        /// Constructor
        /// </summary>
        public AgentPublisher(IMessageBroker messageBroker, IAgentPublisherConfig agentPublisherConfig, INetworkAgentRegistry networkAgentRegistry)
        {
            _agentMovement = new AgentMovement(_agentId);
            _messageBroker = messageBroker;
            _agentPublisherConfig = agentPublisherConfig;

            _pollAndUpdateAgentMovementTimer = new Timer(PollAndUpdateAgentMovement, null, 0, _agentPublisherConfig.PacketUpdateRate);

            _networkAgentRegistry = networkAgentRegistry;
        }

        ~AgentPublisher()
        {
            _pollAndUpdateAgentMovementTimer?.Dispose();
        }

        private void PollAndUpdateAgentMovement(object obj)
        {
            // TODO: also add all player agents
            var agents = _networkAgentRegistry.ControlledAgents.Values;

            foreach (var agent in agents)
            {
                var movementChanges = new List<IMovementEvent>();

                CheckAndUpdateLookDirection(movementChanges, agent);
                CheckAndUpdateInputVector(movementChanges, agent);
                CheckAndUpdateAgentActionData(movementChanges, agent);
                CheckAndUpdateAgentMountData(movementChanges, agent);

                // sending all changes down the broker
                // the handlers will handle them however they see fit
                foreach (var movement in movementChanges)
                {
                    _messageBroker.Publish(this, movement);
                }
            }
        }

        private void CheckAndUpdateLookDirection(IList<IMovementEvent> movementChanges, Agent agent)
        {
            var lookDirection = new LookDirectionChanged(agent);

            if (lookDirection.LookDirection != agent.LookDirection)
            {
                _agentMovement.CalculateMovement(lookDirection);
                movementChanges.Add(lookDirection);
            }
        }

        private void CheckAndUpdateInputVector(IList<IMovementEvent> movementChanges, Agent agent)
        {
            var inputVector = new MovementInputVectorChanged(agent);

            if (inputVector.InputVector != _agentMovement.InputDirection)
            {
                _agentMovement.CalculateMovement(inputVector);
                movementChanges.Add(inputVector);
            }
        }

        private void CheckAndUpdateAgentActionData(IList<IMovementEvent> movementChanges, Agent agent)
        {
            var actionData = new ActionDataChanged(agent);

            if (!actionData.Equals(_agentMovement.ActionData))
            {
                _agentMovement.CalculateMovement(actionData);
                movementChanges.Add(actionData);
            }
        }

        private void CheckAndUpdateAgentMountData(IList<IMovementEvent> movementChanges, Agent agent)
        {
            if (agent.HasMount)
            {
                var mountData = new MountDataChanged(agent);

                if (!mountData.Equals(mountData))
                {
                    _agentMovement.CalculateMovement(mountData);
                    movementChanges.Add(mountData);
                }
            }
        }
    }
}
