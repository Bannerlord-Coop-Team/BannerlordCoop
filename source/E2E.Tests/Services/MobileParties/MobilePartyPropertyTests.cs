using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Armies.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;
public class MobilePartyPropertyTests : IDisposable
{
    E2ETestEnvironment TestEnvironement { get; }
    public MobilePartyPropertyTests(ITestOutputHelper output)
    {
        TestEnvironement = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironement.Dispose();
    }

    [Fact]
    public void ServerChangeCustomName_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironement.Server;

        var clan = GameObjectCreator.CreateInitializedObject<Clan>();
        var party = GameObjectCreator.CreateInitializedObject<MobileParty>();

        var partyId = "PartyId";
        var clanId = "ClanId";

        clan.StringId = clanId;
        party.StringId = partyId;
        party.CustomName = new TextObject("TestCustomName");

        foreach (var client in TestEnvironement.Clients)
        {
            client.ObjectManager.AddExisting(party.StringId, GameObjectCreator.CreateInitializedObject<MobileParty>());
            client.ObjectManager.AddExisting(clan.StringId, GameObjectCreator.CreateInitializedObject<Clan>());
        }

        server.ObjectManager.AddExisting(party.StringId, GameObjectCreator.CreateInitializedObject<MobileParty>());
        server.ObjectManager.AddExisting(clan.StringId, GameObjectCreator.CreateInitializedObject<Clan>());

        // Act
        server.Call(() =>
        {
            party.CustomName = new TextObject("NewTestCustomName");
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(party.StringId, out var clientParty));
            Assert.Equal(clientParty.CustomName.Value, party.CustomName.Value);
        }
    }
}