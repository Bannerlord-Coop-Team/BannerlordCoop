using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobilePartyAis;
public class MobilePartyAiLifetimeTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    public MobilePartyAiLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreate_MobilePartyAi_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var mobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        // Act
        string? aiId = null;
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject(mobilePartyId, out MobileParty mobileParty));
            Assert.True(server.ObjectManager.TryGetId(mobileParty, out _));
            var partyAi = new MobilePartyAi(mobileParty);

            Assert.True(server.ObjectManager.TryGetId(partyAi, out aiId));
        });

        // Assert
        Assert.NotNull(aiId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobilePartyAi>(aiId, out var clientAi));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var clientParty));
            Assert.Same(clientParty, clientAi._mobileParty);
        }
    }

    [Fact]
    public void ServerCreate_MobilePartyAi_UnregisteredOwner_SkipsAiRegistration()
    {
        var server = TestEnvironment.Server;

        server.Call(() =>
        {
            var mobileParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.True(server.ObjectManager.TryGetId(mobileParty, out _));
            Assert.True(server.ObjectManager.Remove(mobileParty));

            var partyAi = new MobilePartyAi(mobileParty);

            Assert.False(server.ObjectManager.TryGetId(partyAi, out _));
        });
    }
}
