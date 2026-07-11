using GameInterface.Services.Entity;
using Missions.Tournaments;
using Moq;

namespace E2E.Tests.Services.Missions.Tournaments;

public class TournamentMissionSessionTests
{
    private static TournamentMissionSession CreateSession(string controllerId)
    {
        var provider = new Mock<IControllerIdProvider>();
        provider.SetupGet(value => value.ControllerId).Returns(controllerId);
        return new TournamentMissionSession(provider.Object);
    }

    [Fact]
    public void HigherRevision_PromotesSuccessorAndPreservesStableSessionIdentity()
    {
        var session = CreateSession("successor");

        Assert.True(session.TryApplyState(
            "session", "mission", 10, 3, "match", "host", new[] { "successor" }));
        Assert.False(session.IsLocalHost);

        Assert.True(session.TryApplyState(
            "session", "mission", 11, 3, "match", "successor", new string[0]));

        Assert.True(session.IsLocalHost);
        Assert.Equal("session", session.SessionId);
        Assert.Equal("mission", session.InstanceId);
        Assert.Equal(11, session.Revision);
    }

    [Fact]
    public void StaleRevision_IsRejectedWithoutRollingAuthorityBack()
    {
        var session = CreateSession("successor");
        Assert.True(session.TryApplyState(
            "session", "mission", 11, 4, "match", "successor", new string[0]));

        Assert.False(session.TryApplyState(
            "session", "mission", 10, 3, "old-match", "host", new[] { "successor" }));

        Assert.True(session.IsLocalHost);
        Assert.Equal("match", session.CurrentMatchId);
        Assert.Equal(4, session.BracketRevision);
    }
}
