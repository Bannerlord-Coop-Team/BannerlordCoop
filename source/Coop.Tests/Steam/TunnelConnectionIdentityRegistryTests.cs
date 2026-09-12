using Coop.Steam;
using Xunit;

namespace Coop.Tests.Steam
{
    public class TunnelConnectionIdentityRegistryTests
    {
        [Fact]
        public void Record_MapsAndUpdatesConnectionIdentity()
        {
            var registry = new TunnelConnectionIdentityRegistry();

            registry.Record(7, 76561198000000001);
            registry.Record(7, 76561198000000002);

            Assert.True(registry.TryGet(7, out var steamId));
            Assert.Equal(76561198000000002ul, steamId);
        }

        [Fact]
        public void RemoveAndZeroIdentity_DoNotExposeStaleIdentity()
        {
            var registry = new TunnelConnectionIdentityRegistry();
            registry.Record(7, 76561198000000001);
            registry.Remove(7);

            Assert.False(registry.TryGet(7, out _));

            registry.Record(8, 76561198000000002);
            registry.Record(8, 0);

            Assert.False(registry.TryGet(8, out _));
        }

        [Fact]
        public void Clear_RemovesEveryConnectionIdentity()
        {
            var registry = new TunnelConnectionIdentityRegistry();
            registry.Record(7, 76561198000000001);
            registry.Record(8, 76561198000000002);

            registry.Clear();

            Assert.False(registry.TryGet(7, out _));
            Assert.False(registry.TryGet(8, out _));
        }
    }
}
