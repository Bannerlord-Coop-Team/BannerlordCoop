using Common.Components;
using Coop.Mod.GameInterfaces.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.GameInterfaces
{
    public class GameInterface : IGameInterface
    {
        public IExampleGameHelper ExampleGameHelper { get; }

        public GameInterface(IExampleGameHelper exampleGameHelper)
        {
            ExampleGameHelper = exampleGameHelper;
        }
    }
}
