using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MBBodyPropertys;
public class MBBodyPropertyLifetimeTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    public MBBodyPropertyLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreate_MBBodyProperty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? MBBodyPropertyId = null;
        server.Call(() =>
        {
            var MBBodyProperty = new MBBodyProperty();

            Assert.True(server.ObjectManager.TryGetId(MBBodyProperty, out MBBodyPropertyId));
        });

        // Assert
        Assert.NotNull(MBBodyPropertyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MBBodyProperty>(MBBodyPropertyId, out var _));
        }
    }

    [Fact]
    public void ClientCreate_MBBodyProperty_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? MBBodyPropertyId = null;

        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            var MBBodyProperty = new MBBodyProperty();

            Assert.False(firstClient.ObjectManager.TryGetId(MBBodyProperty, out MBBodyPropertyId));
        });

        // Assert
        Assert.Null(MBBodyPropertyId);
    }
}
