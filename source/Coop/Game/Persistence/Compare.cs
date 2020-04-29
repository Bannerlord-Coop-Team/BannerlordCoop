using System;

namespace Coop.Game.Persistence
{
    public class Compare
    {
        public const float COORDINATE_PRECISION = 0.001f;
        public const float GAMESPEED_PRECISION = 0.1f;

        public static bool CoordinatesEqual(float a, float b)
        {
            return Math.Abs(a - b) < COORDINATE_PRECISION;
        }

        public static bool GameSpeedEqual(float a, float b)
        {
            return Math.Abs(a - b) < GAMESPEED_PRECISION;
        }
    }
}
