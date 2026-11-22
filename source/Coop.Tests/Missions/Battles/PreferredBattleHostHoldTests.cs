using GameInterface.Services.MapEvents;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Missions.Battles;
using Moq;
using System;
using System.Linq;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class PreferredBattleHostHoldTests
{
    private const string Battle = "MapEvent_1";
    private const string Preferred = "preferred";
    private const string Unlisted = "unlisted";

    private readonly PreferredBattleHostHold hold;

    public PreferredBattleHostHoldTests()
    {
        var playerManager = new Mock<IPlayerManager>();
        playerManager.Setup(manager => manager.Players).Returns(new[]
        {
            new Player(Preferred, "hero_1", "party_1", "clan_1", "character_1"),
            new Player(Unlisted, "hero_2", "party_2", "clan_2", "character_2")
        });
        var preferredHosts = new Mock<IPreferredBattleHosts>();
        preferredHosts.Setup(hosts => hosts.RankOf(It.IsAny<string>())).Returns(PreferredBattleHosts.Unlisted);
        preferredHosts.Setup(hosts => hosts.RankOf(Preferred)).Returns(0);

        hold = new PreferredBattleHostHold(playerManager.Object, Mock.Of<IBattleHostRegistry>(), preferredHosts.Object);
    }

    [Fact]
    public void Request_WhilePreferredHostLoads_IsHeld()
    {
        Assert.Equal(HeldElectionDecision.Held, hold.Request(Battle, Unlisted, null, false, _ => true));
        Assert.True(hold.HasHeldElections);
    }

    [Fact]
    public void Request_WithNobodyRankedAboveLoading_IsNotHeld()
    {
        Assert.Equal(HeldElectionDecision.NotHeld, hold.Request(Battle, Unlisted, null, false, _ => false));
        Assert.False(hold.HasHeldElections);
    }

    [Fact]
    public void PreferredHostRequest_ReleasesInMissionReadyOrder()
    {
        hold.Request(Battle, Unlisted, null, false, _ => true);

        Assert.Equal(HeldElectionDecision.Release, hold.Request(Battle, Preferred, null, false, _ => true));
        Assert.Equal(new[] { Unlisted, Preferred }, hold.Release(Battle).Select(request => request.ControllerId));
        Assert.False(hold.HasHeldElections);
    }

    [Fact]
    public void AwaitedHostLeaving_Releases()
    {
        hold.Request(Battle, Unlisted, null, false, _ => true);

        Assert.Equal(HeldElectionDecision.Release, hold.Drop(Battle, Preferred, _ => true));
    }

    [Fact]
    public void LastHeldRequesterLeaving_DropsTheHold()
    {
        hold.Request(Battle, Unlisted, null, false, _ => true);

        Assert.Equal(HeldElectionDecision.NotHeld, hold.Drop(Battle, Unlisted, _ => true));
        Assert.Null(hold.Release(Battle));
    }

    [Fact]
    public void HoldPastItsWait_IsExpired()
    {
        hold.Wait = TimeSpan.Zero;
        hold.Request(Battle, Unlisted, null, false, _ => true);

        Assert.Equal(new[] { Battle }, hold.ExpiredBattles(DateTime.UtcNow));
    }
}
