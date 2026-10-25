using Missions.Battles;
using System;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>The departed-hull ledger counts each hull once per side, whichever paths report it.</summary>
public class NavalSideHullLedgerTests
{
    // C4: a hull removed with a player who left, then brought back by its captor's record, is one hull gone from its side.
    [Fact]
    public void CountDeparted_TheSameHullTwiceOnOneSide_CountsOnce()
    {
        var ledger = new NavalSideHullLedger();
        var shipId = Guid.NewGuid();

        ledger.CountDeparted(shipId, BattleSideEnum.Defender);
        ledger.CountDeparted(shipId, BattleSideEnum.Defender);

        Assert.Equal(1, ledger.DepartedHulls(BattleSideEnum.Defender));
        Assert.Equal(0, ledger.DepartedHulls(BattleSideEnum.Attacker));
    }

    // A prize captured off one side and later taken away by its captor left both sides.
    [Fact]
    public void CountDeparted_OneHullOffEachSide_CountsForBoth()
    {
        var ledger = new NavalSideHullLedger();
        var shipId = Guid.NewGuid();

        ledger.CountDeparted(shipId, BattleSideEnum.Defender);
        ledger.CountDeparted(shipId, BattleSideEnum.Attacker);
        ledger.CountDeparted(Guid.NewGuid(), BattleSideEnum.Defender);

        Assert.Equal(2, ledger.DepartedHulls(BattleSideEnum.Defender));
        Assert.Equal(1, ledger.DepartedHulls(BattleSideEnum.Attacker));
    }

    // An orphan's side reads as none, which the retreat rule never asks about.
    [Fact]
    public void CountDeparted_OffNoSide_CountsNothing()
    {
        var ledger = new NavalSideHullLedger();

        ledger.CountDeparted(Guid.NewGuid(), BattleSideEnum.None);

        Assert.Equal(0, ledger.DepartedHulls(BattleSideEnum.None));
    }
}
