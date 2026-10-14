using GameInterface.Services.MapEvents.TroopSupply;
using HarmonyLib;
using Missions.Battles;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalMigrationReserveFielderTests
{
    private const string Host = "host";
    private const string Pirates = "MapEventParty_pirates";
    private const string Raiders = "MapEventParty_raiders";

    [Fact]
    public void Tick_OnANonHost_TakesNoReserve()
    {
        var harness = new Harness(isHost: false);
        harness.AddAiHull(Pirates);

        harness.Fielder.Tick();

        harness.Reinforcements.Verify(r => r.TakeMigrationReserve(It.IsAny<BattleSideEnum>()), Times.Never);
    }

    [Fact]
    public void Tick_QueuesEachHullsOwnPartyFirst_ThenAnyParty_AndKeepsWhatDoesNotFit()
    {
        var harness = new Harness(isHost: true);
        var pirateHull = harness.AddAiHull(Pirates, capacity: 3);
        var raiderHull = harness.AddAiHull(Raiders, capacity: 4);
        harness.Recover(Origin(Pirates), Origin(Pirates), Origin(Raiders), Origin(Pirates), Origin(Pirates), Origin(Raiders),
            Origin(Pirates), Origin(Pirates));

        harness.Fielder.Tick();

        Assert.Equal(new[] { Pirates, Pirates, Pirates }, harness.Queued[pirateHull].Select(origin => origin.MapEventPartyId));
        Assert.Equal(new[] { Raiders, Raiders, Pirates, Pirates }, harness.Queued[raiderHull].Select(origin => origin.MapEventPartyId));
        Assert.Equal(1, harness.Fielder.PendingReserve(BattleSideEnum.Defender));

        harness.Capacity[pirateHull] = 4;
        harness.Fielder.Tick();

        Assert.Equal(4, harness.Queued[pirateHull].Count);
        Assert.Equal(0, harness.Fielder.PendingReserve(BattleSideEnum.Defender));
    }

    [Fact]
    public void Tick_WaitsForAHullStillBeingReplaced_ButCountsItsReserveAsPending()
    {
        var harness = new Harness(isHost: true);
        var hull = harness.AddAiHull(Pirates);
        var shipId = harness.Registry.Ships.Single().ShipId;
        harness.Replacing.Add(shipId);
        harness.Recover(Origin(Pirates), Origin(Pirates));

        harness.Fielder.Tick();

        Assert.Empty(harness.Queued[hull]);
        Assert.Equal(2, harness.Fielder.PendingReserve(BattleSideEnum.Defender));

        harness.Replacing.Remove(shipId);
        harness.Fielder.Tick();

        Assert.Equal(2, harness.Queued[hull].Count);
    }

    [Fact]
    public void PendingReserve_StopsCountingOnceNoAfloatAiHullCanTakeIt()
    {
        var harness = new Harness(isHost: true);
        var hull = harness.AddAiHull(Pirates, capacity: 0);
        harness.Recover(Origin(Pirates), Origin(Pirates));
        harness.Fielder.Tick();
        Assert.Equal(2, harness.Fielder.PendingReserve(BattleSideEnum.Defender));
        Assert.Equal(0, harness.Fielder.PendingReserve(BattleSideEnum.Attacker));

        harness.Sinking.Add(hull);

        Assert.Equal(0, harness.Fielder.PendingReserve(BattleSideEnum.Defender));
    }

    [Fact]
    public void Tick_LeavesThePlayersHullsAndAnotherSidesReserveAlone()
    {
        var harness = new Harness(isHost: true);
        var hull = harness.AddAiHull(Pirates);
        harness.Registry.TryRegister(new NetworkShipInfo(Guid.NewGuid(), Host, "MapEventParty_host", false,
            ShipTestHulls.Create(), null));

        harness.Fielder.Tick();

        harness.Reinforcements.Verify(r => r.TakeMigrationReserve(BattleSideEnum.Defender), Times.Once);
        harness.Reinforcements.Verify(r => r.TakeMigrationReserve(BattleSideEnum.Attacker), Times.Never);
        Assert.Single(harness.Queued);
    }

    // A recovered origin carries only the party it was supplied under for this decision.
    private static CoopAgentOrigin Origin(string mapEventPartyId)
    {
        var origin = (CoopAgentOrigin)RuntimeHelpers.GetUninitializedObject(typeof(CoopAgentOrigin));
        AccessTools.Field(typeof(CoopAgentOrigin), "<MapEventPartyId>k__BackingField").SetValue(origin, mapEventPartyId);
        return origin;
    }

    private sealed class Harness
    {
        private readonly List<CoopAgentOrigin> recovered = new();

        public Harness(bool isHost)
        {
            var session = new Mock<IBattleSession>();
            session.SetupGet(s => s.IsLocalHost).Returns(isHost);
            session.Setup(s => s.IsOwn(It.IsAny<string>())).Returns((string id) => id == Host);

            Reinforcements.Setup(r => r.TakeMigrationReserve(BattleSideEnum.Defender)).Returns(() =>
            {
                var taken = recovered.ToList();
                recovered.Clear();
                return taken;
            });
            Reinforcements.Setup(r => r.TakeMigrationReserve(BattleSideEnum.Attacker)).Returns(Array.Empty<CoopAgentOrigin>());

            var engine = new Mock<INavalShipEngine>();
            engine.Setup(e => e.SideOf(It.IsAny<MissionObject>())).Returns((MissionObject hull) =>
                Queued.ContainsKey(hull) ? BattleSideEnum.Defender : BattleSideEnum.Attacker);
            engine.Setup(e => e.IsSinking(It.IsAny<MissionObject>())).Returns((MissionObject hull) => Sinking.Contains(hull));
            engine.Setup(e => e.AddHullReserve(It.IsAny<MissionObject>(), It.IsAny<IReadOnlyList<IAgentOriginBase>>()))
                .Returns((MissionObject hull, IReadOnlyList<IAgentOriginBase> origins) =>
                {
                    int taken = Math.Min(origins.Count, Math.Max(0, Capacity[hull] - Queued[hull].Count));
                    Queued[hull].AddRange(origins.Take(taken).Cast<CoopAgentOrigin>());
                    return taken;
                });

            var replicator = new Mock<IBattleShipReplicator>();
            replicator.Setup(r => r.IsReplacingHull(It.IsAny<Guid>())).Returns((Guid shipId) => Replacing.Contains(shipId));

            Fielder = new NavalMigrationReserveFielder(session.Object, Reinforcements.Object, Registry, engine.Object, replicator.Object);
        }

        public Mock<IReinforcementFielder> Reinforcements { get; } = new();
        public NetworkShipRegistry Registry { get; } = new();
        public Dictionary<MissionObject, List<CoopAgentOrigin>> Queued { get; } = new();
        public Dictionary<MissionObject, int> Capacity { get; } = new();
        public HashSet<MissionObject> Sinking { get; } = new();
        public HashSet<Guid> Replacing { get; } = new();
        public NavalMigrationReserveFielder Fielder { get; }

        public MissionObject AddAiHull(string mapEventPartyId, int capacity = 40)
        {
            var hull = ShipTestHulls.Create();
            Queued[hull] = new List<CoopAgentOrigin>();
            Capacity[hull] = capacity;
            Assert.True(Registry.TryRegister(new NetworkShipInfo(Guid.NewGuid(), Host, mapEventPartyId, true, hull, null)));
            return hull;
        }

        public void Recover(params CoopAgentOrigin[] origins) => recovered.AddRange(origins);
    }
}
