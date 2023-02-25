using Common.Logging;
using Common.Messaging;
using Missions.Services.Network;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// Manages player controlled <see cref="AgentMovement"/>.
    /// </summary>
    public class AgentPublisher
    {
        private Agent _agent;

        private Guid _agentId;

        private AgentMovement _agentMovement;

        private static readonly ILogger _logger = LogManager.GetLogger<AgentPublisher>();

        private IMessageBroker _messageBroker;

        private readonly CancellationTokenSource _agentPollingTaskCancellationTokenSource = new CancellationTokenSource();

        private readonly Task _agentPollingTask;

        private readonly int _packetUpdateRate;

        /// <summary>
        /// Constructor
        /// </summary>
        public AgentPublisher(IMessageBroker messageBroker, int packetUpdateRate)
        {
            _agent = Agent.Main;
            if (NetworkAgentRegistry.Instance.TryGetAgentId(_agent, out _agentId))
            {
                _logger.Warning($"Could not find Agent.Main id");
                _agentId = Guid.NewGuid();

                // TODO: after merge this must be changed to RegisterPlayerAgent
                NetworkAgentRegistry.Instance.RegisterControlledAgent(_agentId, _agent);
            }

            _agentMovement = new AgentMovement(_agentId);
            _messageBroker = messageBroker;

            _agentPollingTask = Task.Run(PollAndUpdateAgentMovement, _agentPollingTaskCancellationTokenSource.Token);
            _packetUpdateRate = packetUpdateRate;
        }

        ~AgentPublisher()
        {
            _agentPollingTaskCancellationTokenSource.Cancel();
            _agentPollingTask.Wait();
        }

        private async Task PollAndUpdateAgentMovement()
        {
            while (!_agentPollingTaskCancellationTokenSource.IsCancellationRequested && Mission.Current != null)
            {
                var movementChanges = new List<IMovementEvent>();

                CheckAndUpdateLookDirection(movementChanges);
                CheckAndUpdateInputVector(movementChanges);
                CheckAndUpdateAgentActionData(movementChanges);
                CheckAndUpdateAgentMountData(movementChanges); 

                // sending all changes down the broker
                // the handlers will handle them however they see fit
                foreach (var movement in movementChanges)
                {
                    _messageBroker.Publish(this, movement);
                }

                // wait a bit and then 
                await Task.Delay(_packetUpdateRate / 3);
            }
        }

        private void CheckAndUpdateLookDirection(IList<IMovementEvent> movementChanges)
        {
            var lookDirection = new LookDirectionChanged(_agent);

            if (lookDirection.LookDirection != _agent.LookDirection)
            {
                _agentMovement.CalculateMovement(lookDirection);
                movementChanges.Add(lookDirection);
            }
        }

        private void CheckAndUpdateInputVector(IList<IMovementEvent> movementChanges)
        {
            var inputVector = new MovementInputVectorChanged(_agent);

            if (inputVector.InputVector != _agentMovement.InputDirection)
            {
                _agentMovement.CalculateMovement(inputVector);
                movementChanges.Add(inputVector);
            }
        }

        private void CheckAndUpdateAgentActionData(IList<IMovementEvent> movementChanges)
        {
            var actionData = new ActionDataChanged(_agent);

            if (!actionData.Equals(_agentMovement.ActionData))
            {
                _agentMovement.CalculateMovement(actionData);
                movementChanges.Add(actionData);
            }
        }

        private void CheckAndUpdateAgentMountData(IList<IMovementEvent> movementChanges)
        {
            if (_agent.HasMount)
            {
                var mountData = new MountDataChanged(_agent);

                if (!mountData.Equals(mountData))
                {
                    _agentMovement.CalculateMovement(mountData);
                    movementChanges.Add(mountData);
                }
            }
        }
    }
}
