using GameInterface.Services.ObjectManager;
using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Handlers;
using Moq;
using Serilog;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments;

public class TournamentHitProgressionCleanupTests
{
    [Fact]
    public void RemoveAcceptedHitProgression_RemovesOnlyTargetSessionEntries()
    {
        var acceptedHitProgression = new HashSet<string>
        {
            "session-a\nmatch-1\ncontroller-1\n1",
            "session-a\nmatch-1\ncontroller-1\n2",
            "session-b\nmatch-1\ncontroller-1\n1"
        };

        TournamentSessionHandler.RemoveAcceptedHitProgression(acceptedHitProgression, "session-a");

        Assert.Single(acceptedHitProgression);
        Assert.Contains("session-b\nmatch-1\ncontroller-1\n1", acceptedHitProgression);
    }

    [Fact]
    public void RemoveSessionTracking_RemovesLiveCombatAndHitProgressionForOnlyTargetSession()
    {
        var liveCombatSessions = new HashSet<string> { "session-a", "session-b" };
        var acceptedHitProgression = new HashSet<string>
        {
            "session-a\nmatch-1\ncontroller-1\n1",
            "session-b\nmatch-1\ncontroller-1\n1"
        };

        TournamentSessionHandler.RemoveSessionTracking(
            liveCombatSessions,
            acceptedHitProgression,
            "session-a");

        Assert.DoesNotContain("session-a", liveCombatSessions);
        Assert.Contains("session-b", liveCombatSessions);
        Assert.Single(acceptedHitProgression);
        Assert.Contains("session-b\nmatch-1\ncontroller-1\n1", acceptedHitProgression);
    }

    [Fact]
    public void TryCreateSessionId_DoesNotRetainGeneratedIdentity()
    {
        var objectManager = new ObjectManager(Mock.Of<ILogger>());

        Assert.True(TournamentGameInterface.TryCreateSessionId(objectManager, out var firstSessionId));
        Assert.True(TournamentGameInterface.TryCreateSessionId(objectManager, out var secondSessionId));

        Assert.NotEqual(firstSessionId, secondSessionId);
        Assert.False(objectManager.Contains(firstSessionId));
        Assert.False(objectManager.Contains(secondSessionId));
    }
}
