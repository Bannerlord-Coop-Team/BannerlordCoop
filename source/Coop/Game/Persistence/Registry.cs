using Coop.Game.Persistence.Party;
using Coop.Game.Persistence.World;
using RailgunNet;
using RailgunNet.Factory;
using RailgunNet.Logic;

namespace Coop.Game.Persistence
{
    public static class Registry
    {
        public static RailRegistry Server(
            IEnvironmentServer environment,
            EntityMapping entityMapping = null)
        {
            RailRegistry reg = new RailRegistry(Component.Server);
            EntityMapping mapping = entityMapping ?? new EntityMapping();

            // Entities
            reg.AddEntityType<WorldEntityServer, WorldState>(new object[] {environment});
            reg.AddEntityType<MobilePartyEntityServer, MobilePartyState>(
                new object[] {environment, mapping.Parties});

            // Events
            reg.AddEventType<EventTimeControl>(new object[] {environment});
            reg.AddEventType<EventPartyMoveTo>();

            // Commands
            reg.SetCommandType<DummyCommand>();

            return reg;
        }

        public static RailRegistry Client(
            IEnvironmentClient environment,
            EntityMapping entityMapping = null)
        {
            RailRegistry reg = new RailRegistry(Component.Client);
            EntityMapping mapping = entityMapping ?? new EntityMapping();

            // Entities
            reg.AddEntityType<WorldEntityClient, WorldState>(new object[] {environment});
            reg.AddEntityType<MobilePartyEntityClient, MobilePartyState>(
                new object[] {environment, mapping.Parties});

            // Events
            reg.AddEventType<EventTimeControl>();
            reg.AddEventType<EventPartyMoveTo>();

            // Commands
            reg.SetCommandType<DummyCommand>();

            return reg;
        }

        public class DummyCommand : RailCommand
        {
        }
    }
}
