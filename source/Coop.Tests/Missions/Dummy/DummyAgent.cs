using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Tests.Missions.Dummy
{
    public class DummyAgent : IUpdateable
    {
        public static event Action<DummyAgent> OnAgentKilled;

        static Random random = new Random();

        public event Action<DummyAgent, double, double> OnMove;

        public double X { get; private set; }
        public double Y { get; private set; }

        public int Priority => 1;

        public DummyAgent()
        {
            X = random.Next(0, 50);
            Y = random.Next(0, 50);
        }

        public DummyAgent(double x, double y)
        {
            X = x;
            Y = y;
        }

        public void Update(TimeSpan frameTime)
        {
            Move(random.NextDouble(), random.NextDouble());
        }

        public void Move(double x, double y)
        {
            X += x;
            Y += y;
            OnMove?.Invoke(this, x, y);
        }

        public void Kill()
        {
            OnAgentKilled?.Invoke(this);
        }

    }
}
