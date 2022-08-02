using Common;
using System;
using System.Collections.Generic;
using System.Data.Services;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Tests.Missions.Dummy
{
    public class DummyMission : IUpdateable
    {
        public int Priority => 0;

        IEnumerable<DummyAgent> Agents;

        public DummyMission(uint amountOfAgents)
        {
            var agents = new List<DummyAgent>();
            for (var i = 0; i < amountOfAgents; i++)
            {
                agents.Add(new DummyAgent());
            }

            Agents = agents;
        }

        public DummyMission(IEnumerable<DummyAgent> agents)
        {
            Agents = agents.ToList();
        }

        public void Update(TimeSpan frameTime)
        {
            foreach (var agent in Agents)
            {
                agent.Update(frameTime);
            }
        }
    }
}
