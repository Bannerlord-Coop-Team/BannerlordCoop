using E2E.Tests.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEventComponents;

public class FieldBattleEventComponentTests : SyncTestBase
{
    public FieldBattleEventComponentTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Server_MapEventParty_Properties()
    {
        string? componentId = null;
        TestEnvironment.Server.Call(() =>
        {
            var mapEvent = new MapEvent();
            var component = new FieldBattleEventComponent(mapEvent);

            Assert.True(Server.ObjectManager.TryGetId(component, out componentId));
        });

        Assert.NotNull(componentId);

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject(componentId, out FieldBattleEventComponent component));
                Assert.NotNull(component);
                Assert.NotNull(component.MapEvent);
            });
        }
    }
}
