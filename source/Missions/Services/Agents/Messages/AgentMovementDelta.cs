using Missions.Services.Agents.Packets;
using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// Represents the delta of an agent's movement between the time it was created and the time it was last updated.
    /// </summary>
    internal sealed class AgentMovementDelta
    {
        private Vec3 _lookDirectionDelta = Vec3.Zero;
        private Vec2 _inputVectorDelta = Vec2.Zero;

        private AgentActionData _actionDataDelta;
        private AgentMountData _mountDataDelta;

        private Vec3 _currentPosition;

        /// <summary>
        /// Guid of the associated <see cref="Agent"/>.
        /// </summary>
        public Guid Guid { get; }

        /// <summary>
        /// The <see cref="Agent"/> whose movement is about to be updated.
        /// </summary>
        public Agent Agent { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="guid"></param>
        public AgentMovementDelta(Agent agent, Guid guid)
        {
            Guid = guid;
            Agent = agent;
            _currentPosition = agent.Position;

            _actionDataDelta = new AgentActionData(agent);
            _mountDataDelta = new AgentMountData(agent);
        }

        /// <summary>
        /// Re-calculate this <see cref="AgentMovementDelta"/>.
        /// </summary>
        /// <param name="change"></param>
        public void CalculateMovement(LookDirectionChanged change)
        {
            _lookDirectionDelta = change.LookDirection;
        }

        /// <summary>
        /// Re-calculate this <see cref="AgentMovementDelta"/>.
        /// </summary>
        /// <param name="change"></param>
        public void CalculateMovement(MovementInputVectorChanged change)
        {
            _inputVectorDelta = change.InputVector;
        }

        /// <summary>
        /// Re-calculate this <see cref="AgentMovementDelta"/>.
        /// </summary>
        /// <param name="change"></param>
        public void CalculateMovement(ActionDataChanged change)
        {
            _actionDataDelta = change.AgentActionData;
        }

        /// <summary>
        /// Re-calculate this <see cref="AgentMovementDelta"/>.
        /// </summary>
        /// <param name="change"></param>
        public void CalculateMovement(MountDataChanged change)
        {
            _mountDataDelta = change.AgentMountData;
        }

        /// <summary>
        /// Generate a <see cref="MovementPacket"/> from this <see cref="AgentMovementDelta"/>.
        /// </summary>
        /// <returns></returns>
        public MovementPacket GetPacket()
        {
            return new MovementPacket(
                Guid, 
                new AgentData(
                    _currentPosition,
                    Agent.GetMovementDirection(),
                    _lookDirectionDelta, 
                    _inputVectorDelta,
                    new AgentEquipmentData(Agent),
                    _actionDataDelta, 
                    _mountDataDelta)
                );
        }
    }
}
