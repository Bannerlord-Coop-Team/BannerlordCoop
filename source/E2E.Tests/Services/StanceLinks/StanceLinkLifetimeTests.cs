using E2E.Tests.Environment;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.StanceLinks;

public class StaceLinkLifetimeTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public StaceLinkLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact(Skip = "Stance link currently disabled")]
    public void ServerCreateStanceLink_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? stanceId = null;
        server.Call(() =>
        {
            var kingdom1 = new Kingdom();
            var stanceLink = new StanceLink(StanceType.War, kingdom1, kingdom1);

            Assert.True(server.ObjectManager.TryGetId(stanceLink, out stanceId));
        });

        // Assert
        Assert.NotNull(stanceId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceId, out var _));
        }
    }

    [Fact(Skip = "Stance link currently disabled")]
    public void ClientCreateStanceLink_DoesNothing()
    {
        // Arrange
        var client1 = TestEnvironment.Clients.First();

        // Act
        string? StanceId = null;
        client1.Call(() =>
        {
            var kingdom1 = new Kingdom();
            var kingdom2 = new Kingdom();
            var stanceLink = new StanceLink(StanceType.War, kingdom1, kingdom2);

            Assert.False(client1.ObjectManager.TryGetId(stanceLink, out StanceId));
        });

        // Assert
        Assert.Null(StanceId);
    }
}