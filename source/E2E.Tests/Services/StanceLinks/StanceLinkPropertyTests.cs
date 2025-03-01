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
    public void Server_KingdomProperties_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? stanceId = null;
        server.Call(() =>
        {
            var kingdom = new Kingdom();
            var clan = new Clan();
            var stanceLink = new StanceLink(StanceType.Neutral, kingdom, clan, true);

            Assert.True(server.ObjectManager.TryGetId(stanceLink, out stanceId));

            stanceLink._stanceType = StanceType.War;
        });

        // Assert
        Assert.NotNull(stanceId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceId, out var _out));
            Assert.True(_out.IsAtWar);
        }

    }
}