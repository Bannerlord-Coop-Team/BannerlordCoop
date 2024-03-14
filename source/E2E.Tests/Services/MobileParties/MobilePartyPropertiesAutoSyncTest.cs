using Autofac;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Utils.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;


public class MobilePartyPropertiesAutoSyncTest
{
   
    E2ETestEnvironment TestEnvironement { get; }
    public MobilePartyPropertiesAutoSyncTest(ITestOutputHelper output)
    {
        TestEnvironement = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironement.Dispose();
    }

    [Fact]
    public void ServerCreateParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironement.Server;
        var partyComponent = GameObjectCreator.CreateInitializedObject<LordPartyComponent>();
        var autosync = server.Container.Resolve<IAutoSync>();

        var prop = AccessTools.Property(typeof(MobileParty), nameof(MobileParty.CustomName));
        autosync.SyncProperty<MobileParty>(prop, party => party.StringId);


        // Act
        string? partyId = null;
        server.Call(() =>
        {
            var party = MobileParty.CreateParty("This should not set", partyComponent, (party) =>
            {
                partyComponent.InitializeLordPartyProperties(party, Vec2.Zero, 0, null);
            });

            partyId = party.StringId;

        });

        // Assert
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var newParty));
        }
    }

    [Fact]
    public void ClientCreateParty_DoesNothing()
    {
        // Arrange
        var server = TestEnvironement.Server;
        var client1 = TestEnvironement.Clients.First();

        var partyComponent = GameObjectCreator.CreateInitializedObject<LordPartyComponent>();

        // Act
        string? partyId = null;
        client1.Call(() =>
        {
            var clientParty = MobileParty.CreateParty("This should not set", partyComponent, (party) =>
            {
                partyComponent.InitializeLordPartyProperties(party, Vec2.Zero, 0, null);
            });

            partyId = clientParty.StringId;
        });

        // Assert
        Assert.False(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));

        foreach (var client in TestEnvironement.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));
        }
    }

}
