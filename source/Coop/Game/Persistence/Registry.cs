using Coop.Game.Persistence.Party;
using Coop.Game.Persistence.World;
using RailgunNet;
using RailgunNet.Factory;
using RailgunNet.Logic;

namespace Coop.Game.Persistence
{
    public static class Registry
    {
        public static RailRegistry Get(
            Component eComponent,
            IEnvironment environment,
            EntityMapping entityMapping = null)
        {
            RailRegistry reg = new RailRegistry(eComponent);
            EntityMapping mapping = entityMapping ?? new EntityMapping();

            switch (eComponent)
            {
                case Component.Client:
                    reg.AddEntityType<WorldEntityClient, WorldState>(new object[] {environment});
                    reg.AddEntityType<MobilePartyEntityClient, MobilePartyState>(
                        new object[] {environment, mapping.Parties});

                    break;
                case Component.Server:
                    reg.AddEntityType<WorldEntityServer, WorldState>(new object[] {environment});
                    reg.AddEntityType<MobilePartyEntityServer, MobilePartyState>(
                        new object[] {environment, mapping.Parties});
                    break;
            }

            reg.SetCommandType<DummyCommand>();
            reg.AddEventType<WorldEventTimeControl>(new object[] {environment});
            reg.AddEventType<EventPartyMoveTo>();

            return reg;
        }

        public class DummyCommand : RailCommand
        {
        }
    }
}
