using Coop.Mod.Missions.Packets.Agents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using TaleWorlds.Library;
using System.Numerics;
using System.Drawing.Drawing2D;
using Xunit.Abstractions;

namespace Coop.Tests
{
    public class InterpolationTest
    {

        private readonly ITestOutputHelper output;

        public InterpolationTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void InterpolationTest1()
        {
            Vec2 position = new Vec2(0, 1);
            Vec3 rotation = new Vec3(0, -1, 0);
            Vec2 currentPosition = new Vec2(0, 0);
            Vec2 inputController = new Vec2(0, 0);

            Vec2 directionVector = MovementHandler.InterpolatePosition(inputController, rotation, currentPosition, position);

            output.WriteLine(directionVector.ToString());
            Assert.True(AlmostEqual(new Vec2(1, 0), directionVector));
        }

        public bool AlmostEqual(Vec2 vec1, Vec2 vec2, double tolerance = 0.01)
        {
            return Math.Abs(vec1.X - vec2.x) < tolerance && Math.Abs(vec1.Y - vec2.y) < tolerance;
        }
        public static Vec2 Rotate(Vec2 v, double radians)
        {
            float sin = MathF.Sin((float)radians);
            float cos = MathF.Cos((float)radians);

            float tx = v.x;
            float ty = v.y;
            v.x = (cos * tx) - (sin * ty);
            v.y = (sin * tx) + (cos * ty);
            return v;
        }
    }
}
