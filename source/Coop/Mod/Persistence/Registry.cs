using Coop.Mod.Persistence.Party;
using Coop.Mod.Persistence.RPC;
using Coop.Mod.Persistence.World;
using RailgunNet;
using RailgunNet.Factory;

namespace Coop.Mod.Persistence
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
            reg.AddEventType<EventTimeControl>();
            reg.AddEventType<EventPartyMoveTo>();
            reg.AddEventType<EventMethodCall>();

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
            reg.AddEventType<EventMethodCall>();

            return reg;
        }
    }
}
