using Coop.Game.Persistence.World;
using RailgunNet;
using RailgunNet.Factory;
using RailgunNet.Logic;

namespace Coop.Game.Persistence
{
    public static class Registry
    {
        public static RailRegistry Get(Component eComponent, IEnvironment environment)
        {
            RailRegistry reg = new RailRegistry(eComponent);

            switch (eComponent)
            {
                case Component.Client:
                    reg.AddEntityType<WorldEntityClient, WorldState>(new object[] {environment});
                    break;
                case Component.Server:
                    reg.AddEntityType<WorldEntityServer, WorldState>(new object[] {environment});
                    break;
            }

            reg.SetCommandType<DummyCommand>();
            reg.AddEventType<WorldEventTimeControl>(new object[] {environment});

            return reg;
        }

        public class DummyCommand : RailCommand
        {
        }
    }
}
