using Coop.Game.Persistence.Party;
using Coop.Game.Persistence.World;
using RailgunNet;
using RailgunNet.Factory;
using RailgunNet.Logic;

namespace Coop.Game.Persistence
{
    public static class Registry
    {
        public static RailRegistry Server(IEnvironmentServer environment)
        {
            RailRegistry reg = new RailRegistry(Component.Server);

            // Entities
            reg.AddEntityType<WorldEntityServer, WorldState>(new object[] {environment});
            reg.AddEntityType<MobilePartyEntityServer, MobilePartyState>(
                new object[] {environment});

            // Events
            reg.AddEventType<EventTimeControl>(new object[] {environment});
            reg.AddEventType<EventPartyMoveTo>();

            return reg;
        }

        public static RailRegistry Client(IEnvironmentClient environment)
        {
            RailRegistry reg = new RailRegistry(Component.Client);

            // Entities
            reg.AddEntityType<WorldEntityClient, WorldState>(new object[] {environment});
            reg.AddEntityType<MobilePartyEntityClient, MobilePartyState>(
                new object[] {environment});

            // Events
            reg.AddEventType<EventTimeControl>();
            reg.AddEventType<EventPartyMoveTo>();

            return reg;
        }
    }
}
