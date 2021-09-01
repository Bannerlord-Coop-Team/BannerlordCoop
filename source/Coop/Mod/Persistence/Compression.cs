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
                m_Compressor = new RailFloatCompressor(
                    0.0f,
                    // TODO #190 - not sure what this does.... probably fine.
                    Campaign.MapMaximumHeight,
                    Compare.COORDINATE_PRECISION / 10.0f);
            }

            [Encoder]
            public void WriteVec2(RailBitBuffer buffer, Vec2 coord)
            {
                buffer.WriteFloat(m_Compressor, coord.X);
                buffer.WriteFloat(m_Compressor, coord.Y);
            }

            [Decoder]
            public Vec2 ReadVec2(RailBitBuffer buffer)
            {
                return new Vec2(buffer.ReadFloat(m_Compressor), buffer.ReadFloat(m_Compressor));
            }
        }
    }
}
