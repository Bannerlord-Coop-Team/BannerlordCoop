using Coop.Mod.Persistence;
using Coop.Mod.Persistence.RPC;
using Coop.Tests.Sync;
using RailgunNet;
using RailgunNet.Factory;
using Sync;
using Xunit;

namespace Coop.Tests.Persistence.RPC
{
    public class EventMethodCall_Test
    {
        private class Foo
        {
            public string LatestArgument;
            public int NumberOfCalls;

            public void SyncedMethod(string sSomeArgument)
            {
                ++NumberOfCalls;
                LatestArgument = sSomeArgument;
            }
        }

        private static RailRegistry CreateRegistry(IEnvironmentClient client)
        {
            RailRegistry reg = new RailRegistry(Component.Client);
            reg.AddEventType<EventMethodCall>(new object[] {client});
            return reg;
        }

        private static RailRegistry CreateRegistry(IEnvironmentServer server)
        {
            RailRegistry reg = new RailRegistry(Component.Server);
            reg.AddEventType<EventMethodCall>(new object[] {server});
            return reg;
        }

        private readonly TestEnvironment m_Environment = new TestEnvironment(
            2,
            CreateRegistry,
            CreateRegistry);

        [Fact]
        private void CanSendEventFromClientToServer()
        {
            // Init patch
            MethodPatch patch = new MethodPatch(typeof(Foo)).Intercept(nameof(Foo.SyncedMethod));
            m_Environment.Persistence.SyncHandlers.Register(
                patch.Methods,
                m_Environment.GetClientAccess(0));

            // Setup an instance & register a handler for it
        }
    }
}
