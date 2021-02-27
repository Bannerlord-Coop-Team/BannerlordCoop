using System;
using RailgunNet.Logic;
using RailgunNet.System.Encoding;
using RemoteAction;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Persistence.Party
{
    /// <summary>
    ///     State for a mobile party entity.
    /// </summary>
    public class MobilePartyState : RailState
    {
        private bool m_IsPlayerControlled;
        private MovementState m_Movement = new MovementState();

        /// <summary>
        ///     Party ID as found in <see cref="TaleWorlds.CampaignSystem.MobileParty.Party.Index" />.
        /// </summary>
        [Immutable]
        public MBGUID PartyId { get; set; } = Coop.InvalidId;

        /// <summary>
        ///     Is the party controlled by any player?
        /// </summary>
        [Mutable]
        public bool IsPlayerControlled
        {
            get => m_IsPlayerControlled;
            set
            {
                if (m_IsPlayerControlled != value)
                {
                    m_IsPlayerControlled = value;
                    OnPlayerControlledChanged?.Invoke();
                }
            }
        }

        /// <summary>
        ///     Movement data for the party.
        /// </summary>
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
        public event Action OnPlayerControlledChanged;
    }

    /// <summary>
    ///     Contains all data relevant to a movement command in serializable format.
    /// </summary>
    public class MovementState
    {
        public MBGUID TargetPartyIndex { get; set; } = Coop.InvalidId;
        public MBGUID SettlementIndex { get; set; } = Coop.InvalidId;
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

        public override string ToString()
        {
            return
                $"Party: {TargetPartyIndex}, Settlement: {SettlementIndex}, Behaviour: {DefaultBehavior}, Position {Position}";
        }
    }

    /// <summary>
    ///     Railgun encoder & decoder for the movement data.
    /// </summary>
    public static class MovementStateSerializer
    {
        public static Compression.Coordinate2d CoordinateCompressor { get; } =
            new Compression.Coordinate2d();

        [Encoder]
        public static void WriteMovementState(this RailBitBuffer buffer, MovementState state)
        {
            buffer.WriteByte((byte) state.DefaultBehavior);
            CoordinateCompressor.WriteVec2(buffer, state.Position);
            buffer.WriteMBGUID(state.TargetPartyIndex);
            buffer.WriteMBGUID(state.SettlementIndex);
        }

        [Decoder]
        public static MovementState ReadMovementState(this RailBitBuffer buffer)
        {
            return new MovementState
            {
                DefaultBehavior = (AiBehavior) buffer.ReadByte(),
                Position = CoordinateCompressor.ReadVec2(buffer),
                TargetPartyIndex = buffer.ReadMBGUID(),
                SettlementIndex = buffer.ReadMBGUID()
            };
        }
    }
}
