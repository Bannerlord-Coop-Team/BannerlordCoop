using E2E.Tests.Environment;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;

public class PartyCreationTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public PartyCreationTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreateParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? partyId = null;
        server.Call(() =>
        {
            var partyComponent = GameObjectCreator.CreateInitializedObject<LordPartyComponent>();
            var clan = GameObjectCreator.CreateInitializedObject<Clan>();
            var party = MobileParty.CreateParty("This should not set", partyComponent, (party) =>
            {
                party.ActualClan = clan;
                partyComponent.InitializeLordPartyProperties(party, Vec2.Zero, 0, null);
            });

            partyId = party.StringId;
        });

        // Assert
        Assert.NotNull(partyId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var newParty));
        }
    }

    [Fact]
    public void ClientCreateParty_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var client1 = TestEnvironment.Clients.First();

        // Act
        string? partyId = null;
        client1.Call(() =>
        {
            var clientParty = MobileParty.CreateParty("This should not set", null, (party) =>
            {
            });

            partyId = clientParty.StringId;
        }, new[] { AccessTools.Method(typeof(MobileParty), nameof(MobileParty.ResetCached)) });

        // Assert
        Assert.False(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var _));
        }
    }
}