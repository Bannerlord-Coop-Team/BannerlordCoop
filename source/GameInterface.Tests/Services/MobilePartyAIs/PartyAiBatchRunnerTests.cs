using Common.Logging;
using Common.Util;
using GameInterface.Services.MobilePartyAIs;
using GameInterface.Services.MobilePartyAIs.Patches;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobilePartyAIs;

public sealed class PartyAiBatchRunnerTests : IDisposable
{
    private readonly ConcurrentQueue<string> logs = new ConcurrentQueue<string>();
    private readonly Action<string> captureLog;

    public PartyAiBatchRunnerTests()
    {
        captureLog = logs.Enqueue;
        OutputSinkManager.AddLogCallback(captureLog);
    }

    public void Dispose() => OutputSinkManager.RemoveLogCallback(captureLog);

    [Fact]
    public void TickParties_ThrowingParty_ContinuesWithRemainingParties()
    {
        MobileParty first = CreateParty();
        MobileParty poisoned = CreateParty();
        MobileParty last = CreateParty();
        var attempts = new List<MobilePartyAi>();
        using var runner = new PartyAiBatchRunner((ai, _) =>
        {
            attempts.Add(ai);
            if (ai == poisoned.Ai)
                throw new InvalidOperationException("poisoned party");
        });

        runner.TickParties(new[] { first, poisoned, last }, 3, 0f);

        Assert.Equal(new[] { first.Ai, poisoned.Ai, last.Ai }, attempts);
    }

    [Fact]
    public void TickParties_MissingAi_ContinuesWithRemainingParties()
    {
        MobileParty missingAi = ObjectHelper.SkipConstructor<MobileParty>();
        MobileParty healthy = CreateParty();
        var attempts = new List<MobilePartyAi>();
        using var runner = new PartyAiBatchRunner((ai, _) => attempts.Add(ai));

        runner.TickParties(new[] { missingAi, healthy }, 2, 0f);

        Assert.Equal(new[] { healthy.Ai }, attempts);
    }

    [Fact]
    public void TickParties_TransientFailure_RetriesOnNextVisit()
    {
        MobileParty party = CreateParty("transient_party");
        int visit = 0;
        var attempts = new List<int>();
        using var runner = new PartyAiBatchRunner((_, _) =>
        {
            attempts.Add(visit);
            if (visit == 1 || visit >= 3)
                throw new InvalidOperationException("transient");
        });

        for (visit = 1; visit <= 4; visit++)
            Visit(runner, party);

        Assert.Equal(new[] { 1, 2, 3, 4 }, attempts);
        Assert.Empty(Warnings("transient_party"));

        // The success on visit 2 reset the count, so visit 5 is the third consecutive failure
        for (; visit <= 6; visit++)
            Visit(runner, party);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, attempts);
        Assert.Single(Errors("transient_party"));
    }

    [Fact]
    public void TickParties_PersistentFailure_RetriesAfterTenSkippedVisits()
    {
        MobileParty party = CreateParty("persistent_party");
        int visit = 0;
        var attempts = new List<int>();
        using var runner = new PartyAiBatchRunner((_, _) =>
        {
            attempts.Add(visit);
            throw new InvalidOperationException("persistent");
        });

        for (visit = 1; visit <= 36; visit++)
            Visit(runner, party);

        Assert.Equal(new[] { 1, 2, 3, 14, 25, 36 }, attempts);
        Assert.Single(Errors("persistent_party"));
    }

    [Fact]
    public void TickParties_QuarantinedParty_LaterPartiesKeepTicking()
    {
        MobileParty first = CreateParty();
        MobileParty poisoned = CreateParty();
        MobileParty last = CreateParty();
        var attempts = new List<MobilePartyAi>();
        using var runner = new PartyAiBatchRunner((ai, _) =>
        {
            attempts.Add(ai);
            if (ai == poisoned.Ai)
                throw new InvalidOperationException("poisoned party");
        });

        for (int visit = 1; visit <= 25; visit++)
            runner.TickParties(new[] { first, poisoned, last }, 3, 0f);

        Assert.Equal(25, attempts.Count(ai => ai == first.Ai));
        Assert.Equal(5, attempts.Count(ai => ai == poisoned.Ai));
        Assert.Equal(25, attempts.Count(ai => ai == last.Ai));
    }

    [Fact]
    public void TickParties_RetrySucceeds_LogsRecoveryWithEpisodeFailures()
    {
        MobileParty party = CreateParty("villager_ES1_1");
        int visit = 0;
        var attempts = new List<int>();
        using var runner = new PartyAiBatchRunner((_, _) =>
        {
            attempts.Add(visit);
            if (visit <= 14)
                throw new InvalidOperationException("broken");
        });

        for (visit = 1; visit <= 26; visit++)
            Visit(runner, party);

        Assert.Equal(new[] { 1, 2, 3, 14, 25, 26 }, attempts);
        string recovery = Assert.Single(Recoveries("villager_ES1_1"));
        Assert.Equal("Mobile-party AI for \"villager_ES1_1\" recovered after 4 failures", recovery);
        Assert.Equal(2, Warnings("villager_ES1_1").Length);
    }

    [Fact]
    public void TickParties_FlappingParty_WarnsOnPowerOfTwoEpisodesAndSecondQuarantine()
    {
        MobileParty party = CreateParty("flapping_party");
        bool broken = false;
        using var runner = new PartyAiBatchRunner((_, _) =>
        {
            if (broken)
                throw new InvalidOperationException("flapping");
        });

        for (int cycle = 0; cycle < 64; cycle++)
        {
            broken = true;
            for (int visit = 0; visit < 13; visit++)
                Visit(runner, party);

            broken = false;
            Visit(runner, party);
        }

        Assert.Single(Errors("flapping_party"));
        Assert.Equal(7, Warnings("flapping_party").Length);
        Assert.Equal(7, Recoveries("flapping_party").Length);
        Assert.Equal(15, logs.Count(l => l.Contains("\"flapping_party\"")));

        // Episode 65 is not a power of two, so its first quarantine is quiet but its second is not
        broken = true;
        for (int visit = 0; visit < 13; visit++)
            Visit(runner, party);
        Assert.Equal(7, Warnings("flapping_party").Length);

        Visit(runner, party);
        Assert.Equal(8, Warnings("flapping_party").Length);
    }

    [Fact]
    public void TickParties_ExceptionTypeChanges_LogsAnotherErrorAndKeepsCounting()
    {
        MobileParty party = CreateParty("type_change_party");
        var failures = new Exception[]
        {
            new InvalidOperationException("first"),
            new TargetInvocationException(new NullReferenceException()),
            new InvalidOperationException("again"),
        };
        int attempts = 0;
        using var runner = new PartyAiBatchRunner((_, _) =>
        {
            Exception failure = failures[Math.Min(attempts, failures.Length - 1)];
            attempts++;
            throw failure;
        });

        for (int visit = 0; visit < 4; visit++)
            Visit(runner, party);

        Assert.Equal(3, attempts);
        Assert.Collection(
            Errors("type_change_party"),
            e => Assert.Contains(Environment.NewLine + typeof(InvalidOperationException).FullName + ": first", e),
            e => Assert.Contains(Environment.NewLine + typeof(TargetInvocationException).FullName + ":", e));
    }

    [Fact]
    public void TickParties_ExceptionMessageThrows_LaterPartiesStillTick()
    {
        MobileParty broken = CreateParty("unreadable_message_party");
        MobileParty healthy = CreateParty();
        int healthyAttempts = 0;
        using var runner = new PartyAiBatchRunner((ai, _) =>
        {
            if (ai == broken.Ai)
                throw new UnreadableMessageException();
            healthyAttempts++;
        });

        for (int visit = 0; visit < 3; visit++)
            runner.TickParties(new[] { broken, healthy }, 2, 0f);

        Assert.Equal(3, healthyAttempts);
        string warning = Assert.Single(Warnings("unreadable_message_party"));
        Assert.Contains(typeof(UnreadableMessageException).FullName!, warning);
    }

    [Fact]
    public void TickParties_MissingAiThenThrowing_LogsBothErrors()
    {
        MobileParty party = ObjectHelper.SkipConstructor<MobileParty>();
        party.StringId = "missing_ai_party";
        using var runner = new PartyAiBatchRunner((_, _) => throw new InvalidOperationException("broken"));

        Visit(runner, party);
        party.Ai = new MobilePartyAi(party);
        Visit(runner, party);

        Assert.Collection(
            Errors("missing_ai_party"),
            e => Assert.Contains("\"Party AI is unavailable\"", e),
            e => Assert.Contains("\"Party AI tick threw\"", e));
    }

    [Fact]
    public void TickParties_BatchesWithoutParty_DoNotShortenQuarantine()
    {
        MobileParty broken = CreateParty();
        MobileParty other = CreateParty();
        int brokenAttempts = 0;
        using var runner = new PartyAiBatchRunner((ai, _) =>
        {
            if (ai != broken.Ai) return;
            brokenAttempts++;
            throw new InvalidOperationException("broken");
        });

        for (int visit = 0; visit < 3; visit++)
            Visit(runner, broken);
        for (int call = 0; call < 20; call++)
            Visit(runner, other);
        for (int visit = 0; visit < 10; visit++)
            Visit(runner, broken);

        Assert.Equal(3, brokenAttempts);

        Visit(runner, broken);

        Assert.Equal(4, brokenAttempts);
    }

    [Fact]
    public void TickParties_NewPartyWithSameStringId_IsNotQuarantined()
    {
        MobileParty removed = CreateParty("caravan_ES1_1");
        MobileParty recreated = CreateParty("caravan_ES1_1");
        var attempts = new List<string>();
        using var runner = new PartyAiBatchRunner((ai, _) =>
        {
            attempts.Add(ai == removed.Ai ? "removed" : "recreated");
            if (ai == removed.Ai)
                throw new InvalidOperationException("broken");
        });

        for (int visit = 0; visit < 3; visit++)
            Visit(runner, removed);
        Visit(runner, recreated);
        Visit(runner, removed);

        Assert.Equal(new[] { "removed", "removed", "removed", "recreated" }, attempts);
    }

    [Fact]
    public void TickParties_QuarantinedPartyRemoved_IsNotRetained()
    {
        int attempts = 0;
        using var runner = new PartyAiBatchRunner((_, _) =>
        {
            attempts++;
            throw new InvalidOperationException("broken");
        });

        WeakReference party = QuarantineNewParty(runner);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.Equal(3, attempts);
        Assert.False(party.IsAlive);
    }

    [Fact]
    public void Dispose_OlderRunner_DoesNotUnbindReplacement()
    {
        var first = new PartyAiBatchRunner();
        var replacement = new PartyAiBatchRunner();

        try
        {
            Assert.Same(replacement, PartiesThinkPatch.BoundRunner);

            first.Dispose();

            Assert.Same(replacement, PartiesThinkPatch.BoundRunner);
        }
        finally
        {
            replacement.Dispose();
            first.Dispose();
        }

        Assert.Null(PartiesThinkPatch.BoundRunner);
    }

    // Kept out of the test body so no local there keeps the party alive during the collection
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference QuarantineNewParty(PartyAiBatchRunner runner)
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.Ai = ObjectHelper.SkipConstructor<MobilePartyAi>();
        var parties = new[] { party };
        for (int visit = 0; visit < 4; visit++)
            runner.TickParties(parties, 1, 0f);

        return new WeakReference(party);
    }

    private static void Visit(PartyAiBatchRunner runner, MobileParty party) =>
        runner.TickParties(new[] { party }, 1, 0f);

    // The sink is shared with tests running in parallel, so lines are matched by party id
    private string[] Errors(string partyId) => Logs($"Skipping mobile-party AI tick for \"{partyId}\":");

    private string[] Warnings(string partyId) => Logs($"Skipping mobile-party AI for \"{partyId}\" for its next");

    private string[] Recoveries(string partyId) => Logs($"Mobile-party AI for \"{partyId}\" recovered");

    private string[] Logs(string prefix) =>
        logs.Where(l => l.StartsWith(prefix, StringComparison.Ordinal)).ToArray();

    private static MobileParty CreateParty(string? stringId = null)
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.StringId = stringId;
        party.Ai = new MobilePartyAi(party);
        return party;
    }

    private sealed class UnreadableMessageException : Exception
    {
        public override string Message => throw new InvalidOperationException("message getter");
    }
}
