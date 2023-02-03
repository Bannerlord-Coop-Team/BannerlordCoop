using Missions.Services.Agents.Packets;
using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    /// <summary>
    /// Represents the delta of an agent's movement in a set time limit.
    /// </summary>
    internal sealed class AgentMovementDelta
    {
        private Vec3 _lookDirectionDelta = Vec3.Zero;
        private Vec2 _inputVectorDelta = Vec2.Zero;

        private AgentActionData _actionDataDelta = null;
        private AgentMountData _mountDataDelta = null;

        private Vec3 _currentPosition;
        public Guid Guid { get; }
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
        }

        public void CalculateMovement(LookDirectionChanged change)
        {
            _lookDirectionDelta += change.LookDirection;
        }        

        public void CalculateMovement(MovementInputVectorChanged change)
        {
            _inputVectorDelta += change.InputVector;
        }

        public void CalculateMovement(ActionDataChanged change)
        {
            if (_actionDataDelta is null)
            {
                _actionDataDelta = change.AgentActionData;
            }
            else
            {
                // TODO: find best way to apply delta
            }
        }

        public void CalculateMovement(MountDataChanged change)
        {
            if (_mountDataDelta is null)
            {
                _mountDataDelta = change.AgentMountData;
            }
            else
            {
                // TODO: find best way to apply delta
            }
        }

        public MovementPacket GetPacketFromDelta()
        {
            return new MovementPacket(Guid, new AgentData(_currentPosition, Agent.GetMovementDirection(), _lookDirectionDelta, _inputVectorDelta, null, _actionDataDelta, _mountDataDelta));
        }
    }
}
