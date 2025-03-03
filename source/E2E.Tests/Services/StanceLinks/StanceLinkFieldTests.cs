using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using System.Reflection;
using Common.Util;
using HarmonyLib;

namespace E2E.Tests.Services.StanceLinks;
public class StanceLinkFieldsTests : SyncTestBase
{
    private readonly List<MethodBase> disabledMethods;
    E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    public StanceLinkFieldsTests(ITestOutputHelper output) : base(output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    [Fact]
    public void ServerChangeStanceLink_SyncAllClients()
    {
        // Arrange
        var field = AccessTools.Field(typeof(StanceLink), nameof(StanceLink._stanceType));
        var intercept = TestEnvironment.GetIntercept(field);
        var newValue = StanceType.Alliance;
        var oldValue = StanceType.War;
        string? StanceLinkId = null;

    // Act
    Server.Call(() =>
        {
            var kingdom = new Kingdom();
            var clan = new Clan();
            var serverStanceLink = new StanceLink(oldValue, kingdom, clan, false);

            Assert.True(Server.ObjectManager.TryGetId(serverStanceLink, out StanceLinkId));

            /// Simulate the field changing
            intercept.Invoke(null, new object[] { serverStanceLink, newValue });
            Assert.Equal(newValue, serverStanceLink._stanceType);
        });

        // Assert
        Assert.NotNull(StanceLinkId);
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(StanceLinkId, out var clientStanceLink));
            Assert.Equal(newValue, clientStanceLink._stanceType);
        }
    }

    


}