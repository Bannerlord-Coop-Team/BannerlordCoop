using System;
using RailgunNet.System.Encoding;
using RailgunNet.System.Encoding.Compressors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Mod.Persistence
{
    /// <summary>
    ///     Provides Railgun data compression for TaleWorld classes
    /// </summary>
    public class Compression
    {
        /// <summary>
        ///     Map size is limited, we do not need the full float range.
        /// </summary>
        public class Coordinate2d
        {
            private readonly RailFloatCompressor m_Compressor;

            public Coordinate2d()
            {
                float min = Math.Min(Campaign.MapMinimumPosition.x, Campaign.MapMinimumPosition.y);
                float max = Math.Max(Campaign.MapMaximumPosition.x, Campaign.MapMaximumPosition.y);
                m_Compressor = new RailFloatCompressor(
                    min,
                    max,
                    Compare.COORDINATE_PRECISION / 10.0f);
            }

            [Encoder]
            public void WriteVec2(RailBitBuffer buffer, Vec2 coord)
            {
                buffer.WriteFloat(m_Compressor, coord.x);
                buffer.WriteFloat(m_Compressor, coord.y);
            }

            [Decoder]
            public Vec2 ReadVec2(RailBitBuffer buffer)
            {
                return new Vec2(buffer.ReadFloat(m_Compressor), buffer.ReadFloat(m_Compressor));
            }
        }
    }
}
