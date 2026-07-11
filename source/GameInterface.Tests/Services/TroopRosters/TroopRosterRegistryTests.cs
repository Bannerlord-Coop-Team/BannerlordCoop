using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters;
using HarmonyLib;
using Moq;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.TroopRosters;

public class TroopRosterRegistryTests
{
    [Fact]
    public void RegisterMapEventCasualtyRosters_RegistersEveryRosterUnderStableOwnerIds()
    {
        var graph = CreateMapEventGraph("battle", partyCountPerSide: 2);
        var objectManager = CreateObjectManager();
        var registry = new TestTroopRosterRegistry(objectManager, graph.MapEvent);

        registry.RegisterMapEventCasualtyRosters(new[] { graph.MapEvent });

        for (int i = 0; i < graph.Parties.Count; i++)
        {
            int partyNumber = i + 1;
            AssertRoster(objectManager, $"MapEventParty_battle_{partyNumber}_WoundedInBattle", graph.Parties[i]._woundedInBattle);
            AssertRoster(objectManager, $"MapEventParty_battle_{partyNumber}_DiedInBattle", graph.Parties[i]._diedInBattle);
            AssertRoster(objectManager, $"MapEventParty_battle_{partyNumber}_RoutedInBattle", graph.Parties[i]._routedInBattle);
        }
    }

    [Fact]
    public void RegisterAllObjectsWithRemap_AdoptsServerIdsForMapEventCasualtyRosters()
    {
        var serverGraph = CreateMapEventGraph("battle", partyCountPerSide: 1);
        var serverObjectManager = CreateObjectManager();
        var serverRegistry = new TestTroopRosterRegistry(serverObjectManager, serverGraph.MapEvent);

        RegisterServerRoster(serverObjectManager, "TroopRoster_aggregate_wounded", serverGraph.Parties[0]._woundedInBattle);
        RegisterServerRoster(serverObjectManager, "TroopRoster_aggregate_died", serverGraph.Parties[0]._diedInBattle);
        RegisterServerRoster(serverObjectManager, "TroopRoster_aggregate_routed", serverGraph.Parties[0]._routedInBattle);

        var remap = new Dictionary<string, string>();
        serverRegistry.CollectIdRemap(remap);

        Assert.Equal("TroopRoster_aggregate_wounded", remap["TroopRoster_MapEventParty_battle_1_WoundedInBattle"]);
        Assert.Equal("TroopRoster_aggregate_died", remap["TroopRoster_MapEventParty_battle_1_DiedInBattle"]);
        Assert.Equal("TroopRoster_aggregate_routed", remap["TroopRoster_MapEventParty_battle_1_RoutedInBattle"]);

        var clientGraph = CreateMapEventGraph("battle", partyCountPerSide: 1);
        var clientObjectManager = CreateObjectManager();
        var clientRegistry = new TestTroopRosterRegistry(clientObjectManager, clientGraph.MapEvent);

        clientRegistry.RegisterAllObjectsWithRemap(remap);

        AssertRoster(clientObjectManager, "aggregate_wounded", clientGraph.Parties[0]._woundedInBattle);
        AssertRoster(clientObjectManager, "aggregate_died", clientGraph.Parties[0]._diedInBattle);
        AssertRoster(clientObjectManager, "aggregate_routed", clientGraph.Parties[0]._routedInBattle);
        Assert.False(clientObjectManager.Contains("TroopRoster_MapEventParty_battle_1_WoundedInBattle"));
        Assert.False(clientObjectManager.Contains("TroopRoster_MapEventParty_battle_1_DiedInBattle"));
        Assert.False(clientObjectManager.Contains("TroopRoster_MapEventParty_battle_1_RoutedInBattle"));
    }

    private static MapEventGraph CreateMapEventGraph(string mapEventId, int partyCountPerSide)
    {
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        mapEvent.StringId = mapEventId;
        mapEvent._sides = new MapEventSide[2];

        var parties = new List<MapEventParty>();
        for (int sideIndex = 0; sideIndex < mapEvent._sides.Length; sideIndex++)
        {
            var side = ObjectHelper.SkipConstructor<MapEventSide>();
            var sideParties = new MBList<MapEventParty>();
            AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._battleParties)).SetValue(side, sideParties);

            for (int partyIndex = 0; partyIndex < partyCountPerSide; partyIndex++)
            {
                var party = ObjectHelper.SkipConstructor<MapEventParty>();
                party._woundedInBattle = ObjectHelper.SkipConstructor<TroopRoster>();
                party._diedInBattle = ObjectHelper.SkipConstructor<TroopRoster>();
                party._routedInBattle = ObjectHelper.SkipConstructor<TroopRoster>();
                sideParties.Add(party);
                parties.Add(party);
            }

            mapEvent._sides[sideIndex] = side;
        }

        return new MapEventGraph(mapEvent, parties);
    }

    private static IObjectManager CreateObjectManager() =>
        new global::GameInterface.Services.ObjectManager.ObjectManager(Mock.Of<ILogger>());

    private static void RegisterServerRoster(IObjectManager objectManager, string id, TroopRoster roster) =>
        Assert.True(objectManager.AddExisting(id, roster));

    private static void AssertRoster(IObjectManager objectManager, string id, TroopRoster expected)
    {
        Assert.True(objectManager.TryGetObject<TroopRoster>(id, out var actual));
        Assert.Same(expected, actual);
    }

    private sealed record MapEventGraph(MapEvent MapEvent, IReadOnlyList<MapEventParty> Parties);

    private sealed class TestTroopRosterRegistry : TroopRosterRegistry
    {
        private readonly IEnumerable<MapEvent> mapEvents;

        public TestTroopRosterRegistry(IObjectManager objectManager, params MapEvent[] mapEvents)
            : base(Mock.Of<ILogger>(), Mock.Of<IAutoRegistryFactory>(), objectManager)
        {
            this.mapEvents = mapEvents;
        }

        public override void RegisterAllObjects() => RegisterMapEventCasualtyRosters(mapEvents);
    }
}
