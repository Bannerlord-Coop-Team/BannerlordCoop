using Common.Network;
using Common.Network.Coalescing;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes
{
    /// <summary>
    /// End to end tests for the per-tick coalesced Hero.Gold sync. The server buffers each Gold set and
    /// flushes once per tick, so repeated sets within a tick collapse to a single latest-wins send and a
    /// client converges to the server's final value.
    /// </summary>
    public class HeroGoldCoalescingTests : SyncTestBase
    {
        private readonly string HeroId;

        public HeroGoldCoalescingTests(ITestOutputHelper output) : base(output)
        {
            HeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        }

        [Fact]
        public void Server_GoldSet_BuffersUntilFlush()
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(HeroId, out var hero));
                hero.Gold = 500;
            });

            // Coalesced: the send is buffered, so clients still hold the old value until the tick flush.
            Assert.True(Server.Resolve<ISendCoalescer>().HasPending);
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(HeroId, out var clientHero));
                Assert.Equal(0, clientHero.Gold);
            }

            TestEnvironment.FlushCoalescer();

            Assert.False(Server.Resolve<ISendCoalescer>().HasPending);
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(HeroId, out var clientHero));
                Assert.Equal(500, clientHero.Gold);
            }
        }

        [Fact]
        public void Server_MultipleGoldSetsInOneTick_ClientConvergesToLatest()
        {
            // Three sets in one tick collapse to one latest-wins send; the client lands on the final value.
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(HeroId, out var hero));
                hero.Gold = 100;
                hero.Gold = 250;
                hero.Gold = 777;
            });

            TestEnvironment.FlushCoalescer();

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(HeroId, out var hero));
                Assert.Equal(777, hero.Gold);
            });

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(HeroId, out var clientHero));
                Assert.Equal(777, clientHero.Gold);
            }
        }
    }
}
