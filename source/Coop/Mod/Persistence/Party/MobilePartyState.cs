using System;
using RailgunNet.Logic;
using RailgunNet.System.Encoding;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Persistence.Party
{
    public class MobilePartyState : RailState
    {
        private MovementState m_Movement = new MovementState();
        [Immutable] public int PartyId { get; set; }

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
        public static MBGUID InvalidIndex = new MBGUID(0xDEADBEEF);
        public MBGUID TargetPartyIndex { get; set; } = InvalidIndex;
        public MBGUID SettlementIndex { get; set; } = InvalidIndex;
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

            return DefaultBehavior == other.DefaultBehavior &&
                   TargetPartyIndex == other.TargetPartyIndex &&
                   SettlementIndex == other.SettlementIndex;
        }

        public override int GetHashCode()
        {
            // TODO: What's supposed to happen here? The Equals override is necessary because of the coordinates comparision.
            return base.GetHashCode();
        }
    }

    public static class MovementStateSerializer
    {
        private static Compression.Coordinate2d CoordinateCompressor { get; } =
            new Compression.Coordinate2d();

        [Encoder]
        public static void WriteMovementState(this RailBitBuffer buffer, MovementState state)
        {
            buffer.WriteByte((byte) state.DefaultBehavior);
            CoordinateCompressor.Write(buffer, state.Position);
            buffer.WriteUInt(state.TargetPartyIndex.InternalValue);
            buffer.WriteUInt(state.SettlementIndex.InternalValue);
        }

        [Decoder]
        public static MovementState ReadMovementState(this RailBitBuffer buffer)
        {
            return new MovementState
            {
                DefaultBehavior = (AiBehavior) buffer.ReadByte(),
                Position = CoordinateCompressor.Read(buffer),
                TargetPartyIndex = new MBGUID(buffer.ReadUInt()),
                SettlementIndex = new MBGUID(buffer.ReadUInt())
            };
        }
    }
}
