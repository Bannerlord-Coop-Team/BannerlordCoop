using System;
using RailgunNet.System.Encoding;
using RailgunNet.System.Encoding.Compressors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Game.Persistence
{
    public class Compression
    {
        public class Coordinate2d
        {
            private readonly RailFloatCompressor m_Compressor;

            public Coordinate2d()
            {
                m_Compressor = new RailFloatCompressor(
                    0.0f,
                    Math.Max(Campaign.MapWidth, Campaign.MapHeight),
                    Compare.COORDINATE_PRECISION / 10.0f);
            }

            [Encoder]
            public void Write(RailBitBuffer buffer, Vec2 coord)
            {
                buffer.WriteFloat(m_Compressor, coord.X);
                buffer.WriteFloat(m_Compressor, coord.Y);
            }

            [Decoder]
            public Vec2 Read(RailBitBuffer buffer)
            {
                return new Vec2(buffer.ReadFloat(m_Compressor), buffer.ReadFloat(m_Compressor));
            }
        }
    }
}
