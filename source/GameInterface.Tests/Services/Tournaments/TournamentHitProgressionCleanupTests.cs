using GameInterface.Services.Tournaments.Handlers;
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
}
