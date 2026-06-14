using Coop.Core.Server.Services.Instances;
using Coop.Tests.Mocks;
using LiteNetLib;
using Xunit;

namespace Coop.Tests.Server.Services.Instances
{
    public class InstanceCoordinatorTests
    {
        private const string Settlement = "town_ES3";
        private const string Tavern = "tavern";
        private const string TownCenter = "center";

        private readonly InstanceCoordinator coordinator = new InstanceCoordinator();
        private readonly TestNetwork network = new TestNetwork();

        [Fact]
        public void FirstPeer_CreatesInstance_AndBecomesHost()
        {
            var peer = network.CreatePeer();

            var result = coordinator.Join(peer, Settlement, Tavern);

            Assert.True(result.BecameHost);
            Assert.NotEqual(default, result.InstanceId);
        }

        [Fact]
        public void SecondPeer_SameLocation_JoinsSameInstance_NotHost()
        {
            var first = coordinator.Join(network.CreatePeer(), Settlement, Tavern);
            var second = coordinator.Join(network.CreatePeer(), Settlement, Tavern);

            Assert.Equal(first.InstanceId, second.InstanceId);
            Assert.False(second.BecameHost);
        }

        [Fact]
        public void DifferentLocation_GetsDifferentInstance()
        {
            var tavern = coordinator.Join(network.CreatePeer(), Settlement, Tavern);
            var center = coordinator.Join(network.CreatePeer(), Settlement, TownCenter);

            Assert.NotEqual(tavern.InstanceId, center.InstanceId);
        }

        [Fact]
        public void NonHostLeaving_DoesNotMigrateHost()
        {
            coordinator.Join(network.CreatePeer(), Settlement, Tavern);
            var member = network.CreatePeer();
            coordinator.Join(member, Settlement, Tavern);

            var result = coordinator.Leave(member);

            Assert.True(result.WasMember);
            Assert.Null(result.NewHost);
        }

        [Fact]
        public void HostLeaving_WithMembersRemaining_ReElectsHost()
        {
            var host = network.CreatePeer();
            var member = network.CreatePeer();
            coordinator.Join(host, Settlement, Tavern);
            coordinator.Join(member, Settlement, Tavern);

            var result = coordinator.Leave(host);

            Assert.True(result.WasMember);
            Assert.Equal(member, result.NewHost);
        }

        [Fact]
        public void LastMemberLeaving_RetiresInstance_NewJoinGetsNewId()
        {
            var peer = network.CreatePeer();
            var first = coordinator.Join(peer, Settlement, Tavern);
            coordinator.Leave(peer);

            var rejoin = coordinator.Join(network.CreatePeer(), Settlement, Tavern);

            Assert.NotEqual(first.InstanceId, rejoin.InstanceId);
            Assert.True(rejoin.BecameHost);
        }

        [Fact]
        public void JoiningNewLocation_LeavesPreviousInstance()
        {
            var peer = network.CreatePeer();
            var other = network.CreatePeer();

            // peer is alone in the tavern, so it is the host there.
            coordinator.Join(peer, Settlement, Tavern);
            // peer moves to the town center; the tavern instance should now be empty.
            coordinator.Join(peer, Settlement, TownCenter);

            // A fresh peer entering the tavern becomes host of a brand-new instance.
            var tavernRejoin = coordinator.Join(other, Settlement, Tavern);
            Assert.True(tavernRejoin.BecameHost);
        }

        [Fact]
        public void Leave_PeerNeverJoined_ReportsNotMember()
        {
            var result = coordinator.Leave(network.CreatePeer());

            Assert.False(result.WasMember);
        }
    }
}
