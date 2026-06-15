using Coop.Core.Server.Services.Instances;
using Coop.Tests.Mocks;
using LiteNetLib;
using Xunit;

namespace Coop.Tests.Server.Services.Instances
{
    public class MissionManagerTests
    {
        private const string Settlement = "town_ES3";
        private const string Tavern = "tavern";
        private const string TownCenter = "center";

        private readonly MissionManager manager = new MissionManager();
        private readonly TestNetwork network = new TestNetwork();

        [Fact]
        public void FirstPeer_CreatesInstance_AndBecomesHost()
        {
            var peer = network.CreatePeer();

            var result = manager.Join(peer, Settlement, Tavern);

            Assert.True(result.BecameHost);
            Assert.NotEqual(default, result.InstanceId);
        }

        [Fact]
        public void SecondPeer_SameLocation_JoinsSameInstance_NotHost()
        {
            var first = manager.Join(network.CreatePeer(), Settlement, Tavern);
            var second = manager.Join(network.CreatePeer(), Settlement, Tavern);

            Assert.Equal(first.InstanceId, second.InstanceId);
            Assert.False(second.BecameHost);
        }

        [Fact]
        public void SamePeer_ReentersSameLocation_KeepsSameInstance()
        {
            // PlayerEnteredLocation fires several times per entry; re-joining the same location must
            // not churn a new instance id (which would invalidate the client's in-flight NAT punch).
            var peer = network.CreatePeer();
            var first = manager.Join(peer, Settlement, Tavern);
            var second = manager.Join(peer, Settlement, Tavern);
            var third = manager.Join(peer, Settlement, Tavern);

            Assert.Equal(first.InstanceId, second.InstanceId);
            Assert.Equal(first.InstanceId, third.InstanceId);
            // Still the (lone) host across re-entries.
            Assert.True(second.BecameHost);
        }

        [Fact]
        public void DifferentLocation_GetsDifferentInstance()
        {
            var tavern = manager.Join(network.CreatePeer(), Settlement, Tavern);
            var center = manager.Join(network.CreatePeer(), Settlement, TownCenter);

            Assert.NotEqual(tavern.InstanceId, center.InstanceId);
        }

        [Fact]
        public void NonHostLeaving_DoesNotMigrateHost()
        {
            manager.Join(network.CreatePeer(), Settlement, Tavern);
            var member = network.CreatePeer();
            manager.Join(member, Settlement, Tavern);

            var result = manager.Leave(member);

            Assert.True(result.WasMember);
            Assert.Null(result.NewHost);
        }

        [Fact]
        public void HostLeaving_WithMembersRemaining_ReElectsHost()
        {
            var host = network.CreatePeer();
            var member = network.CreatePeer();
            manager.Join(host, Settlement, Tavern);
            manager.Join(member, Settlement, Tavern);

            var result = manager.Leave(host);

            Assert.True(result.WasMember);
            Assert.Equal(member, result.NewHost);
        }

        [Fact]
        public void LastMemberLeaving_RetiresInstance_NewJoinGetsNewId()
        {
            var peer = network.CreatePeer();
            var first = manager.Join(peer, Settlement, Tavern);
            manager.Leave(peer);

            var rejoin = manager.Join(network.CreatePeer(), Settlement, Tavern);

            Assert.NotEqual(first.InstanceId, rejoin.InstanceId);
            Assert.True(rejoin.BecameHost);
        }

        [Fact]
        public void JoiningNewLocation_LeavesPreviousInstance()
        {
            var peer = network.CreatePeer();
            var other = network.CreatePeer();

            // peer is alone in the tavern, so it is the host there.
            manager.Join(peer, Settlement, Tavern);
            // peer moves to the town center; the tavern instance should now be empty.
            manager.Join(peer, Settlement, TownCenter);

            // A fresh peer entering the tavern becomes host of a brand-new instance.
            var tavernRejoin = manager.Join(other, Settlement, Tavern);
            Assert.True(tavernRejoin.BecameHost);
        }

        [Fact]
        public void Leave_PeerNeverJoined_ReportsNotMember()
        {
            var result = manager.Leave(network.CreatePeer());

            Assert.False(result.WasMember);
        }
    }
}
