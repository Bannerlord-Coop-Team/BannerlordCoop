using System;
using RailgunNet.Logic;
using TaleWorlds.Library;

namespace Coop.Game.Persistence.Party
{
    public class MobilePartyState : RailState
    {
        private Vec2 m_Position = Vec2.Zero;
        [Immutable] public int PartyId { get; set; }

        [Mutable]
        [Compressor(typeof(Compression.Coordinate2d))]
        public Vec2 Position
        {
            get => m_Position;
            set
            {
                if (!Compare.CoordinatesEqual(m_Position, value))
                {
                    m_Position = value;
                    OnPositionChanged?.Invoke();
                }
            }
        }

        public event Action OnPositionChanged;
    }
}
