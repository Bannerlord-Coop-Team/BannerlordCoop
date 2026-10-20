using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using Moq;
using System;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit;

namespace Coop.Tests.GameInterface.Services.MapEvents;

/// <summary>The naval rules for a departed host or member read the election's record once the map event is gone.</summary>
public class BattleHostRetentionPolicyTests
{
    private const string MapEventId = "battle";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithoutTheMapEvent_TheRulesFollowTheElectionsRecord(bool naval)
    {
        var policy = Create(new BattleHostAssignment("host", Array.Empty<string>(), epoch: 1, isNaval: naval));

        Assert.Equal(naval, policy.DisconnectWithdraws(MapEventId));
        Assert.Equal(naval, policy.KeepsDepartedHostReport(MapEventId));
        Assert.Equal(naval, policy.KeepsHostAfterResult(MapEventId, hasResult: true));
        Assert.False(policy.KeepsHostAfterResult(MapEventId, hasResult: false));
        Assert.False(policy.DefersHostFinalize(MapEventId));
    }

    // C1: only the naval host kept after its result is refused. A member, or a host still owing its result (one rejoining
    // before its old peer timed out), may enter.
    [Theory]
    [InlineData(true, true, "host", true)]
    [InlineData(true, true, "member", false)]
    [InlineData(true, false, "host", false)]
    [InlineData(false, false, "host", false)]
    public void RefusesEntry_OnlyToTheKeptNavalHost(bool naval, bool kept, string controllerId, bool refused)
    {
        var policy = Create(new BattleHostAssignment("host", new[] { "member" }, epoch: 1, isNaval: naval,
            keptAfterResult: kept));

        Assert.Equal(refused, policy.RefusesEntry(MapEventId, controllerId));
    }

    [Fact]
    public void WithNoElectionAndNoMapEvent_NothingIsNaval()
    {
        var policy = Create(null);

        Assert.False(policy.DisconnectWithdraws(MapEventId));
        Assert.False(policy.RefusesEntry(MapEventId, "host"));
    }

    // A policy whose map event no longer resolves, with the server's host assignment when one exists.
    private static BattleHostRetentionPolicy Create(BattleHostAssignment? assignment)
    {
        var objectManager = new Mock<IObjectManager>();
        MapEvent? mapEvent = null;
        objectManager.Setup(manager => manager.TryGetObject(MapEventId, out mapEvent)).Returns(false);
        var registry = new Mock<IBattleHostRegistry>();
        registry.Setup(hosts => hosts.TryGet(MapEventId, out assignment)).Returns(assignment != null);
        return new BattleHostRetentionPolicy(objectManager.Object, registry.Object);
    }
}
