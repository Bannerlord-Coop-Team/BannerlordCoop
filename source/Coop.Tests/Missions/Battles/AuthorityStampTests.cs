using Missions.Battles;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class AuthorityStampTests
{
    private const string Own = "local";
    private const string Peer = "peer";
    private static readonly string[] InMission = { Peer, "someone_else" };

    // The hull streams' held-latest order: host epoch first, then the sender's revision.
    [Theory]
    [InlineData(1, 5, 1, 4, true)]
    [InlineData(1, 4, 1, 4, false)]
    [InlineData(1, 3, 1, 4, false)]
    [InlineData(2, 1, 1, 9, true)]
    [InlineData(1, 9, 2, 1, false)]
    public void IsNewer_OrdersByHostEpochThenRevision(int epoch, long revision, int thanEpoch, long thanRevision, bool expected)
    {
        var stamp = new AuthorityStamp(Peer, epoch, revision);

        Assert.Equal(expected, stamp.IsNewer(new AuthorityStamp(Peer, thanEpoch, thanRevision)));
    }

    // Station uses carry no epoch, and each owner numbers its own.
    [Theory]
    [InlineData(null, 0, "old", 1, true)]
    [InlineData("old", 3, "old", 4, true)]
    [InlineData("old", 3, "old", 3, false)]
    [InlineData("old", 3, "old", 2, false)]
    [InlineData("old", 3, "new", 1, true)]
    public void IsNewer_WithinOneEpoch_AcceptsALaterRevisionOrAnotherSendersFirst(string? thanSender, long than,
        string sender, long revision, bool expected)
    {
        var stamp = new AuthorityStamp(sender, 0, revision);

        Assert.Equal(expected, stamp.IsNewer(new AuthorityStamp(thanSender!, 0, than)));
    }

    [Fact]
    public void IsNewer_AnOlderEpochLosesEvenFromAnotherSender()
    {
        Assert.False(new AuthorityStamp("departed", 1, 99).IsNewer(new AuthorityStamp("host", 2, 1)));
    }

    [Theory]
    [InlineData(Peer, Peer, 5, 0, 4, 0, false, null)]
    [InlineData(Peer, "someone_else", 5, 0, 4, 0, false, "not_authority")]
    [InlineData(Own, Own, 5, 0, 4, 0, false, "own_hull")]
    [InlineData(Peer, Peer, 4, 0, 4, 0, false, "stale")]
    [InlineData(Peer, Peer, 5, 1, 4, 9, false, null)]
    [InlineData(Peer, Peer, 3, 9, 4, 1, false, "stale")]
    [InlineData(Peer, Peer, 9, 2, 1, 3, true, "stale_epoch")]
    [InlineData(Peer, Peer, 1, 4, 9, 3, true, null)]
    [InlineData(Peer, Peer, 9, 3, 9, 3, true, "stale")]
    public void Validate_OrdersTheAuthoritysRevisions_AndRestartsThemOnANewerEpochOnlyWhenFollowingTheHost(string authority,
        string sender, long revision, int epoch, long acceptedRevision, int acceptedEpoch, bool followsHostEpoch, string? expected)
    {
        var stamp = new AuthorityStamp(sender, epoch, revision);

        Assert.Equal(expected, stamp.Validate(authority, Own, new AuthorityStamp(authority, acceptedEpoch, acceptedRevision),
            followsHostEpoch, InMission));
    }

    [Theory]
    [InlineData(Peer, Peer, null)]
    [InlineData(Peer, "someone_else", "not_authority")]
    [InlineData(Own, Own, "own_hull")]
    [InlineData(Own, Peer, "own_hull")]
    public void ValidateSender_DropsOwnHullsAndSendersOtherThanTheAuthority(string authority, string sender, string? expected)
    {
        Assert.Equal(expected, AuthorityStamp.ValidateSender(sender, authority, Own, InMission));
    }

    // R7: one gate for every hull, condition, station and weapon stream: a sender not in the mission is dropped, and only a
    // departed host's held state, replayed on purpose, skips it.
    [Fact]
    public void ValidateSender_DropsTheAuthorityOnceItIsNotInTheMission_AndNoMembersSkipTheGate()
    {
        Assert.Equal("not_in_mission", AuthorityStamp.ValidateSender("departed", "departed", Own, InMission));
        Assert.Equal("not_in_mission", new AuthorityStamp("departed", 0, 1).Validate("departed", Own,
            new AuthorityStamp("departed", 0, 0), followsHostEpoch: false, InMission));
        Assert.Null(AuthorityStamp.ValidateSender("departed", "departed", Own, null!));
        Assert.Null(AuthorityStamp.ValidateMember(Peer, InMission));
    }
}
