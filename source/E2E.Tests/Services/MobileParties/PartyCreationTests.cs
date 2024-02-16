using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Partyes;

public class PartyCreationTests : IDisposable
{
    E2ETestEnvironement TestEnvironement { get; }
    public PartyCreationTests(ITestOutputHelper output)
    {
        TestEnvironement = new E2ETestEnvironement(output);
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

        // Act
        MobileParty? serverParty = null;
        server.Call(() =>
        {
            serverParty = MobileParty.CreateParty("This should not set", partyComponent, (party) =>
            {
                AccessTools.Method(typeof(LordPartyComponent), "InitializeLordPartyProperties")
                    .Invoke(partyComponent, new object[] { party, Vec2.Zero, 0, null });
            });
        });

        // Assert
        var newPartyStringId = serverParty?.StringId;
        Assert.NotNull(newPartyStringId);

        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(newPartyStringId, out var newParty));
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
        MobileParty? clientParty = null;
        client1.Call(() =>
        {
            clientParty = MobileParty.CreateParty("This should not set", partyComponent, (party) =>
            {
                AccessTools.Method(typeof(LordPartyComponent), "InitializeLordPartyProperties")
                    .Invoke(partyComponent, new object[] { party, Vec2.Zero, 0, null });
            });
        });

        var newPartyStringId = clientParty?.StringId;

        // Assert
        Assert.False(server.ObjectManager.TryGetObject<MobileParty>(newPartyStringId, out var _));

        foreach (var client in TestEnvironement.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MobileParty>(newPartyStringId, out var _));
        }
    }
}