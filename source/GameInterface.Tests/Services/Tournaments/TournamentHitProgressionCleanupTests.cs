using GameInterface.Services.ObjectManager;
using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Handlers;
using GameInterface.Services.Tournaments.Messages;
using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments;

public class TournamentHitProgressionCleanupTests
{
    [Theory]
    [InlineData(-1f, true)]
    [InlineData(-1.01f, false)]
    public void ProgressionValidation_AcceptsOnlyVanillaMeleeSentinelBelowZero(
        float shotDifficulty,
        bool expected)
    {
        TournamentHitProgressionData progression = CreateProgression(
            "player",
            "player",
            1,
            shotDifficulty);

        Assert.Equal(expected, TournamentSessionHandler.IsValidProgressionData(progression));
    }

    [Fact]
    public void HitProgressionDedupeKey_UsesDamageOriginAcrossHostMigration()
    {
        TournamentHitProgressionData oldHost = CreateProgression("player", "old-host", 1);
        TournamentHitProgressionData newHost = CreateProgression("player", "new-host", 1);

        Assert.NotEqual(
            TournamentSessionHandler.GetHitProgressionDedupeKey(oldHost),
            TournamentSessionHandler.GetHitProgressionDedupeKey(newHost));
    }

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

    private static TournamentHitProgressionData CreateProgression(
        string attackerControllerId,
        string damageOriginControllerId,
        long damageSequence,
        float shotDifficulty = 0f) =>
        new(
            "session",
            "match",
            1,
            1,
            attackerControllerId,
            damageOriginControllerId,
            damageSequence,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            -1,
            1f,
            shotDifficulty,
            0.5f,
            10f,
            0,
            false,
            false,
            false,
            false,
            false);
}
