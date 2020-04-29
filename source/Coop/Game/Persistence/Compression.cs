using RailgunNet.System.Encoding.Compressors;

namespace Coop.Game.Persistence
{
    public class Compression
    {
        public static readonly RailFloatCompressor GameSpeed = new RailFloatCompressor(
            1.0f,
            10.0f,
            Compare.GAMESPEED_PRECISION / 10.0f);

        public static readonly RailFloatCompressor Coordinate = new RailFloatCompressor(
            -512.0f,
            512.0f,
            Compare.COORDINATE_PRECISION / 10.0f);
    }
}
