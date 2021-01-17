using Coop.Mod.Persistence.Party;
using Coop.Mod.Persistence.MethodCall;
using Coop.Mod.Persistence.World;
using RailgunNet;
using RailgunNet.Factory;
using RemoteAction;

namespace Coop.Mod.Persistence
{
    /// <summary>
    ///     Factory methods to create the Railgun registry for client & server.
    /// </summary>
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
            reg.AddEventType<EventMethodCall>(new object[] {environment});

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
            reg.AddEventType<EventMethodCall>(new object[] {environment});

            return reg;
        }
    }
}
