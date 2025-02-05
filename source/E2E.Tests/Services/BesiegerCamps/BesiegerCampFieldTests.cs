using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using Xunit.Abstractions;

namespace E2E.Tests.Services.BesiegerCamps;

public class BesiegerCampFieldTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }

    private EnvironmentInstance Server => TestEnvironment.Server;

    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    private readonly string besiegerCampId;

    public BesiegerCampFieldTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        var BesiegerCamp = ObjectHelper.SkipConstructor<BesiegerCamp>();

        // Create BesiegerCamp on the server
        Assert.True(Server.ObjectManager.AddNewObject(BesiegerCamp, out besiegerCampId));

        // Create BesiegerCamp on all clients
        foreach (var client in Clients)
        {
            var clientBesiegerCamp = ObjectHelper.SkipConstructor<BesiegerCamp>();
            Assert.True(client.ObjectManager.AddExisting(besiegerCampId, clientBesiegerCamp));
        }
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerChangeBesiegerCampLeaderParty()
    {
        // Arrange
        var field = AccessTools.Field(typeof(BesiegerCamp), nameof(BesiegerCamp._leaderParty));
        var intercept = TestEnvironment.GetIntercept(field);

        /// Create instances on server
        Assert.True(Server.ObjectManager.AddNewObject(ObjectHelper.SkipConstructor<MobileParty>(), out var mobilePartyId));

        /// Create instances on all clients
        foreach (var client in Clients)
        {
            var clientParty = ObjectHelper.SkipConstructor<MobileParty>();
            Assert.True(client.ObjectManager.AddExisting(mobilePartyId, clientParty));
        }

        // Act
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var BesiegerCamp));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var serverParty));

            Assert.Null(BesiegerCamp._leaderParty);

            /// Simulate the field changing
            intercept.Invoke(null, new object[] { BesiegerCamp, serverParty });

            Assert.Same(serverParty, BesiegerCamp._leaderParty);
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var BesiegerCamp));

            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var clientParty));

            Assert.True(clientParty == BesiegerCamp._leaderParty);
        }
    }
}