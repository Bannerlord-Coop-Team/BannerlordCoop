using Common.Network.Coalescing;
using Common.Util;
using Coop.Core.Server.Services.SiegeEvents;
using Coop.Tests.Mocks;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEvents;
using HarmonyLib;
using Moq;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace Coop.Tests.Server.Services.SiegeEvents;

public class SiegeSacrificeTests
{
    private readonly TestNetwork network = new();
    private readonly Mock<IObjectManager> objects = new();
    private readonly Mock<ISiegeBreakOut> action = new();
    private readonly Mock<ISendCoalescer> coalescer = new();
    private readonly MobileParty party = ObjectHelper.SkipConstructor<MobileParty>();
    private readonly CharacterObject troop = ObjectHelper.SkipConstructor<CharacterObject>();
    private readonly TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
    private readonly List<string> calls = new();
    private readonly SiegeSacrifice sacrifice;

    public SiegeSacrificeTests()
    {
        var partyBase = ObjectHelper.SkipConstructor<PartyBase>();
        AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Party)).SetValue(party, partyBase);
        AccessTools.Property(typeof(PartyBase), nameof(PartyBase.MemberRoster)).SetValue(partyBase, roster);
        uint rosterId = 4;
        objects.Setup(manager => manager.TryGetHandleWithLogging(roster, out rosterId)).Returns(true);
        objects.Setup(manager => manager.TryGetHandle(roster, out rosterId)).Returns(true);
        uint troopId = 5;
        objects.Setup(manager => manager.TryGetHandleWithLogging(troop, out troopId)).Returns(true);
        action.Setup(service => service.ApplySacrifice(party, It.IsAny<int>(), out It.Ref<int>.IsAny))
            .Returns((MobileParty current, int losses, out int armyLosses) =>
            {
                calls.Add("sacrifice");
                armyLosses = -1;
                current.MemberRoster.AddToCounts(troop, -losses);
                var casualties = TroopRoster.CreateDummyTroopRoster();
                casualties.AddToCounts(troop, losses);
                return casualties;
            });
        sacrifice = new SiegeSacrifice(objects.Object, action.Object, network, coalescer.Object);
    }

    [Fact]
    public void AffordableSacrifice_MarksPaidFirst_TakesTheTroopsAndFlushesTheRoster()
    {
        roster.AddToCounts(troop, 10);

        var paid = sacrifice.TryApply(party, 4, () => calls.Add("paid"));

        Assert.NotNull(paid);
        Assert.Equal(new[] { 5u }, paid.CharacterIds);
        Assert.Equal(new[] { 4 }, paid.Counts);
        Assert.Equal(-1, paid.ArmyCasualties);
        Assert.Equal(6, roster.TotalRegulars);
        Assert.Equal(new[] { "paid", "sacrifice" }, calls);
        coalescer.Verify(sending => sending.FlushInstance(4u, network), Times.Once);
    }

    [Fact]
    public void ExactlyEnoughRegulars_IsPaid()
    {
        roster.AddToCounts(troop, 5);

        Assert.NotNull(sacrifice.TryApply(party, 5, () => calls.Add("paid")));
        Assert.Equal(0, roster.TotalRegulars);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void TooFewRegulars_IsRefusedWithoutMarkingPaidOrTakingTroops(int available)
    {
        roster.AddToCounts(troop, available);

        Assert.Null(sacrifice.TryApply(party, 5, () => calls.Add("paid")));

        Assert.Empty(calls);
        Assert.Equal(available, roster.TotalRegulars);
        action.Verify(service => service.ApplySacrifice(It.IsAny<MobileParty>(), It.IsAny<int>(), out It.Ref<int>.IsAny), Times.Never);
    }

    [Fact]
    public void MissingRosterRegistration_IsRefusedWithoutMarkingPaid()
    {
        roster.AddToCounts(troop, 10);
        uint rosterId = 4;
        objects.Setup(manager => manager.TryGetHandleWithLogging(roster, out rosterId)).Returns(false);

        Assert.Null(sacrifice.TryApply(party, 4, () => calls.Add("paid")));

        Assert.Empty(calls);
        Assert.Equal(10, roster.TotalRegulars);
    }

    [Fact]
    public void UnregisteredRegularTroop_IsRefusedWithoutMarkingPaid()
    {
        roster.AddToCounts(troop, 10);
        uint troopId = 5;
        objects.Setup(manager => manager.TryGetHandleWithLogging(troop, out troopId)).Returns(false);

        Assert.Null(sacrifice.TryApply(party, 4, () => calls.Add("paid")));

        Assert.Empty(calls);
        Assert.Equal(10, roster.TotalRegulars);
    }
}
