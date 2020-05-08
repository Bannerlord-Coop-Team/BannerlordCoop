using System;
using RailgunNet.System.Encoding;
using RailgunNet.System.Encoding.Compressors;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence
{
    public class Compression
    {
        public class Coordinate : RailFloatCompressor
        {
            public Coordinate() : base(
                0.0f,
                Math.Max(Campaign.MapWidth, Campaign.MapHeight),
                Compare.COORDINATE_PRECISION / 10.0f)
            {
            }

            [Encoder(Encoders.SupportedType.Float_t)]
            public void Write(RailBitBuffer buffer, float f)
            {
                buffer.WriteFloat(this, f);
            }

            [Decoder(Encoders.SupportedType.Float_t)]
            public float Read(RailBitBuffer buffer)
            {
                return buffer.ReadFloat(this);
            }
        }
    }
}
