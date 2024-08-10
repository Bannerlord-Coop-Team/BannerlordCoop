using E2E.Tests.Environment;
using E2E.Tests.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyComponents;
public class PartyComponentTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public PartyComponentTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerChange_MobileParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        PartyComponent? component = null;
        MobileParty? newParty = null;
        server.Call(() =>
        {
            component = GameObjectCreator.CreateInitializedObject<MobileParty>().PartyComponent;
            newParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
        });

        Assert.NotNull(component);
        Assert.NotNull(newParty);

        // Act
        server.Call(() =>
        {
            component.MobileParty = newParty;
        });

        // Assert

        Assert.True(server.ObjectManager.TryGetId(component, out var componentId));

        Assert.Equal(component.MobileParty.StringId, newParty.StringId);
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<PartyComponent>(componentId, out var clientComponent));
            Assert.Equal(clientComponent.MobileParty.StringId, newParty.StringId);
        }
    }

    [Fact]
    public void ClientChange_MobileParty_NoChange()
    {
        // Arrange
        var server = TestEnvironment.Server;

        PartyComponent? component = null;
        MobileParty? newParty = null;
        server.Call(() =>
        {
            component = GameObjectCreator.CreateInitializedObject<MobileParty>().PartyComponent;
            newParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
        });

        Assert.NotNull(component);
        Assert.NotNull(newParty);

        Assert.True(server.ObjectManager.TryGetId(component, out var componentId));

        // Act
        var firstClient = TestEnvironment.Clients.First();

        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<PartyComponent>(componentId, out var clientComponent));
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(newParty.StringId, out var clientNewParty));

            clientComponent.MobileParty = clientNewParty;
        });

        // Assert

        Assert.NotEqual(component.MobileParty.StringId, newParty.StringId);
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<PartyComponent>(componentId, out var clientComponent));
            Assert.NotEqual(clientComponent.MobileParty.StringId, newParty.StringId);
        }
    }
}
