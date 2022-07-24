using System;
using RailgunNet.Logic;
using RailgunNet.System.Encoding;
using RemoteAction;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
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

        [Mutable]
        public MapVec2 MapPosition
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
        public event Action OnPlayerControlledChanged;

        #region Private

        private bool m_IsPlayerControlled;
        private MapVec2 m_MapPosition = new MapVec2(Vec2.Invalid);

        #endregion
    }

    /// <summary>
    ///     Contains all data relevant to a movement order in a Railgun serializable format.
    /// </summary>
    public class MovementState
    {
        public Guid TargetPartyIndex { get; set; } = Coop.InvalidId;
        public Guid SettlementIndex { get; set; } = Coop.InvalidId;
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
            buffer.WriteGUID(state.TargetPartyIndex);
            buffer.WriteGUID(state.SettlementIndex);
        }

        [Decoder]
        public static MovementState ReadMovementState(this RailBitBuffer buffer)
        {
            return new MovementState
            {
                DefaultBehavior = (AiBehavior) buffer.ReadByte(),
                TargetPosition = CoordinateCompressor.ReadVec2(buffer),
                TargetPartyIndex = buffer.ReadGUID(),
                SettlementIndex = buffer.ReadGUID()
            };
        }
    }

    /// <summary>
    ///     Describes a position on the campaign map. Basically just a wrapper around <see cref="Vec2" /> so it can be
    ///     used in Railgun with its own encoder / decoder and custom precision for campaign map coordinates.
    /// </summary>
    public readonly struct MapVec2
    {
        public static implicit operator MapVec2(Vec2 v)
        {
            return new MapVec2(v);
        }

        public static implicit operator Vec2(MapVec2 p)
        {
            return p.Vec2;
        }

        public MapVec2(Vec2 pos)
        {
            Vec2 = pos;
        }

        public Vec2 Vec2 { get; }

        public override bool Equals(object obj)
        {
            return obj is MapVec2 position && Equals(position);
        }

        private bool Equals(MapVec2 other)
        {
            return Compare.CoordinatesEqual(Vec2, other.Vec2);
        }

        public override int GetHashCode()
        {
            return Vec2.GetHashCode();
        }
    }

    /// <summary>
    ///     Railgun encoder & decoder for a <see cref="MapVec2" />.
    /// </summary>
    public static class MapVec2Serializer
    {
        public static Compression.Coordinate2d CoordinateCompressor { get; } =
            new Compression.Coordinate2d();

        [Encoder]
        public static void WriteMapVec2(this RailBitBuffer buffer, MapVec2 state)
        {
            CoordinateCompressor.WriteVec2(buffer, state.Vec2);
        }

        [Decoder]
        public static MapVec2 ReadMapVec2(this RailBitBuffer buffer)
        {
            return new MapVec2(CoordinateCompressor.ReadVec2(buffer));
        }
    }
}