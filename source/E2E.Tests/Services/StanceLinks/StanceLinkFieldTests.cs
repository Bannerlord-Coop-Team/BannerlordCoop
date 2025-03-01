using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.StanceLinks;
public class StanceLinkPropertyTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public StanceLinkPropertyTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void Server_StanceLinkProperties_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? stanceId = null;
        string? kingdomId = null;
        string? clanId = null;
        server.Call(() =>
        {
            var kingdom = new Kingdom();
            var clan = new Clan();
            var stanceLink = new StanceLink(StanceType.Neutral, kingdom, clan, true);

            Assert.True(server.ObjectManager.TryGetId(kingdom, out kingdomId));
            Assert.True(server.ObjectManager.TryGetId(clan, out clanId));
            Assert.True(server.ObjectManager.TryGetId(stanceLink, out stanceId));

            stanceLink.StanceType = StanceType.War;
            stanceLink.Faction1 = clan;
            stanceLink.Faction2 = kingdom;
        });

        // Assert
        Assert.NotNull(kingdomId);
        Assert.NotNull(clanId);
        Assert.NotNull(stanceId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceId, out var _out));

            client.ObjectManager.TryGetId(_out.Faction1, out var faction1Id);
            client.ObjectManager.TryGetId(_out.Faction2, out var faction2Id);

            Assert.Equal(clanId, faction1Id);
            Assert.Equal(kingdomId, faction2Id);
            Assert.True(_out.IsAtWar);
            
        }

    }
}