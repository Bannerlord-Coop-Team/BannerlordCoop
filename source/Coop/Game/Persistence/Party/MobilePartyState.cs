using System;
using System.Text;
using RailgunNet.Logic;
using RailgunNet.System.Encoding;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Game.Persistence.Party
{
    public class MobilePartyState : RailState
    {
        [Immutable] public int PartyId { get; set; }

        private MovementState m_Movement = new MovementState();
        [Mutable]
        public MovementState Movement
        {
            get => m_Movement;
            set
            {
                if (!m_Movement.Equals(value))
                {
                    m_Movement = value;
                    OnMovementChanged?.Invoke();
                }
            }
        }
        public event Action OnMovementChanged;
    }

    public class MovementState
    {
        public AiBehavior DefaultBehavior { get; set; }
        public Vec2 Position { get; set; }
        public override bool Equals(object obj)
        {
            MovementState other = obj as MovementState;
            if (other == null)
            {
                return false;
            }

            if (!Compare.CoordinatesEqual(Position, other.Position))
            {
                return false;
            }

            return DefaultBehavior == other.DefaultBehavior;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public static class MovementStateSerializer
    {
        private static Compression.Coordinate2d CoordinateCompressor { get; } = new Compression.Coordinate2d();

        [Encoder]
        public static void Encoder(this RailBitBuffer buffer, MovementState state)
        {
            buffer.WriteByte((byte) state.DefaultBehavior);
            CoordinateCompressor.Write(buffer, state.Position);
        }

        [Decoder]
        public static MovementState Decode(this RailBitBuffer buffer)
        {
            return new MovementState()
            {
                DefaultBehavior = (AiBehavior) buffer.ReadByte(),
                Position = CoordinateCompressor.Read(buffer)
            };
        }
    }
}
