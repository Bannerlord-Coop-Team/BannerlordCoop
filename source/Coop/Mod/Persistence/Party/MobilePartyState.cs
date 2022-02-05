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
        /// <summary>
        ///     Party ID as found in <see cref="TaleWorlds.CampaignSystem.MobileParty.Party.Index" />.
        /// </summary>
        [Immutable]
        public Guid PartyId { get; set; } = Guid.Empty;

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

        [Mutable]
        public MapPosition MapPosition
        {
            get => m_MapPosition;
            set
            {
                if (!m_MapPosition.Equals(value))
                {
                    m_MapPosition = value;
                    OnPositionChanged?.Invoke();
                }
            }
        }

        public event Action OnPositionChanged;
        public event Action OnMovementChanged;
        public event Action OnPlayerControlledChanged;

        #region Private

        private bool m_IsPlayerControlled;
        private MovementState m_Movement = new MovementState();
        private MapPosition m_MapPosition = new MapPosition(Vec2.Invalid);

        #endregion
    }

    /// <summary>
    ///     Contains all data relevant to a movement order in a Railgun serializable format.
    /// </summary>
    public class MovementState
    {
        public MBGUID TargetPartyIndex { get; set; } = Coop.InvalidId;
        public MBGUID SettlementIndex { get; set; } = Coop.InvalidId;
        public AiBehavior DefaultBehavior { get; set; }
        public Vec2 TargetPosition { get; set; }

        public override bool Equals(object obj)
        {
            MovementState other = obj as MovementState;
            if (other == null)
            {
                return false;
            }

            if (!Compare.CoordinatesEqual(TargetPosition, other.TargetPosition))
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
                $"Party: {TargetPartyIndex}, Settlement: {SettlementIndex}, Behaviour: {DefaultBehavior}, Position {TargetPosition}";
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
            CoordinateCompressor.WriteVec2(buffer, state.TargetPosition);
            buffer.WriteMBGUID(state.TargetPartyIndex);
            buffer.WriteMBGUID(state.SettlementIndex);
        }

        [Decoder]
        public static MovementState ReadMovementState(this RailBitBuffer buffer)
        {
            return new MovementState
            {
                DefaultBehavior = (AiBehavior) buffer.ReadByte(),
                TargetPosition = CoordinateCompressor.ReadVec2(buffer),
                TargetPartyIndex = buffer.ReadMBGUID(),
                SettlementIndex = buffer.ReadMBGUID()
            };
        }
    }

    /// <summary>
    ///     Describes a position on the campaign map. Basically just a wrapper around <see cref="Vec2" /> so it can be
    ///     used in Railgun with its own encoder / decoder and custom precision for campaign map coordinates.
    /// </summary>
    public readonly struct MapPosition
    {
        public static implicit operator MapPosition(Vec2 v)
        {
            return new MapPosition(v);
        }

        public static implicit operator Vec2(MapPosition p)
        {
            return p.Vec2;
        }

        public MapPosition(Vec2 pos)
        {
            Vec2 = pos;
        }

        public Vec2 Vec2 { get; }

        public override bool Equals(object obj)
        {
            return obj is MapPosition position && Equals(position);
        }

        private bool Equals(MapPosition other)
        {
            return Compare.CoordinatesEqual(Vec2, other.Vec2);
        }

        public override int GetHashCode()
        {
            return Vec2.GetHashCode();
        }
    }

    /// <summary>
    ///     Railgun encoder & decoder for a <see cref="MapPosition" />.
    /// </summary>
    public static class MapPositionSerializer
    {
        public static Compression.Coordinate2d CoordinateCompressor { get; } =
            new Compression.Coordinate2d();

        [Encoder]
        public static void WriteMovementState(this RailBitBuffer buffer, MapPosition state)
        {
            CoordinateCompressor.WriteVec2(buffer, state.Vec2);
        }

        [Decoder]
        public static MapPosition ReadMovementState(this RailBitBuffer buffer)
        {
            return new MapPosition(CoordinateCompressor.ReadVec2(buffer));
        }
    }
}