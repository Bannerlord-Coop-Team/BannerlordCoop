using Missions.Battles;
using Missions.Messages;
using Moq;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalSideTallyReaderTests
{
    private const int Afloat = 0;

    private readonly Mock<INavalShipEngine> engine = new Mock<INavalShipEngine>();
    private readonly Mock<IBattleShipConditionStream> conditions = new Mock<IBattleShipConditionStream>();
    private readonly Mock<INavalMigrationReserveFielder> migrationReserve = new Mock<INavalMigrationReserveFielder>();
    private readonly List<MissionObject> hulls = new List<MissionObject>();

    public NavalSideTallyReaderTests()
    {
        engine.SetupGet(e => e.Hulls).Returns(hulls);
    }

    // The end check counts each hull by the engine's side and sinking state, its crew by hull and its owner-reported reserve.
    [Fact]
    public void Count_ReadsEachHullsSideSinkingStateCrewAndReportedReserve()
    {
        var afloat = Hull(BattleSideEnum.Attacker, Afloat, reserve: 5);
        var sunk = Hull(BattleSideEnum.Attacker, BattleShipCondition.Sunk, reserve: 7);
        var enemy = Hull(BattleSideEnum.Defender, Afloat, reserve: 9);
        migrationReserve.Setup(m => m.PendingReserve(BattleSideEnum.Attacker)).Returns(3);
        var crew = new Dictionary<MissionObject, int> { [afloat] = 2, [sunk] = 4, [enemy] = 6 };

        var tally = Reader().Count(BattleSideEnum.Attacker, liveHumans: 11, crew);

        Assert.Equal(11, tally.LiveHumans);
        Assert.Equal(2, tally.Hulls);
        Assert.Equal(1, tally.HullsAfloat);
        Assert.Equal(2, tally.CrewAboard);
        Assert.Equal(1, tally.HullsOutOfAction);
        Assert.Equal(5 + 3, tally.Reserve);
    }

    // A capture's orphan has no team, so the engine reads its side as none and neither side counts it.
    [Fact]
    public void Count_SkipsAHullWithNoSide()
    {
        Hull(BattleSideEnum.None, Afloat, reserve: 20);

        Assert.Equal(0, Reader().Count(BattleSideEnum.Attacker, liveHumans: 0, NavalSideTallyReader.NoCrew).Hulls);
        Assert.Equal(0, Reader().Count(BattleSideEnum.Defender, liveHumans: 0, NavalSideTallyReader.NoCrew).Hulls);
    }

    private NavalSideTallyReader Reader() => new NavalSideTallyReader(engine.Object, conditions.Object, migrationReserve.Object);

    private MissionObject Hull(BattleSideEnum side, int sinkingState, int reserve)
    {
        var hull = ShipTestHulls.Create();
        hulls.Add(hull);
        engine.Setup(e => e.SideOf(hull)).Returns(side);
        engine.Setup(e => e.ReadCondition(hull)).Returns(new BattleShipCondition(100f, 100f, 0f, null, sinkingState));
        conditions.Setup(c => c.HullReserve(hull)).Returns(reserve);
        return hull;
    }
}
