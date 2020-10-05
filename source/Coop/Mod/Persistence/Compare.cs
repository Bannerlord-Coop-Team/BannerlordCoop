using System;
using TaleWorlds.Library;

namespace Coop.Mod.Persistence
{
    /// <summary>
    ///     Comparison methods for data that is being exchanged.
    /// </summary>
    public class Compare
    {
        public const float COORDINATE_PRECISION = 0.001f;

        public static bool CoordinatesEqual(float a, float b)
        {
            return Math.Abs(a - b) < COORDINATE_PRECISION;
        }

        public static bool CoordinatesEqual(Vec2 a, Vec2 b)
        {
            return CoordinatesEqual(a.X, b.X) && CoordinatesEqual(a.Y, b.Y);
        }

        public static bool Equals(Type t, object a, object b)
        {
            if (t == typeof(Vec2))
            {
                return CoordinatesEqual((Vec2) a, (Vec2) b);
            }

            return a.Equals(b);
        }
    }
}
