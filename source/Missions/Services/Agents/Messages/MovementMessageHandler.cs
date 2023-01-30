using Missions.Services.Agents.Packets;
using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Messages
{
    internal sealed class MovementMessageHandler
    {
        private Vec3 _lookDirectionDelta = Vec3.Zero;
        private Vec2 _inputVectorDelta = Vec2.Zero;
        private AgentActionData _actionDataDelta = null;
        private AgentMountData _mountDataDelta = null;
        private Vec3 _currentPosition = Vec3.Zero;
        private Guid _guid;
        private Agent _agent;

        public void CalculateMovement(LookDirectionChanged change)
        {
            _lookDirectionDelta += change.LookDirection;
        }        

        public void CalculateMovement(MovementInputVectorChanged change)
        {
            _inputVectorDelta += change.InputVector;
        }

        public void CalculateMovement(AgentActionData change)
        {
            if (_actionDataDelta is null)
            {
                _actionDataDelta = change;
            }
            else
            {
                // TODO: find best way to apply delta
            }
        }

        public void CalculateMovement(AgentMountData change)
        {
            if (_mountDataDelta is null)
            {
                _mountDataDelta = change;
            }
            else
            {
                // TODO: find best way to apply delta
            }
        }

        public MovementPacket GetPacketFromDelta()
        {
            return new MovementPacket(_guid, new AgentData(_currentPosition, _agent.GetMovementDirection(), _lookDirectionDelta, _inputVectorDelta, null, _actionDataDelta, _mountDataDelta));
        }
    }
}
