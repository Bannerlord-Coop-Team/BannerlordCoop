using Common.Util;
using GameInterface.Services.MobilePartyAIs;
using GameInterface.Services.MobilePartyAIs.Patches;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobilePartyAIs;

public sealed class PartyAiBatchRunnerTests
{
    private readonly CaptureSink sink = new CaptureSink();
    private readonly ILogger logger;

    public PartyAiBatchRunnerTests()
    {
        logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
    }

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
        MobileParty party = CreateParty();
        int visit = 0;
        var attempts = new List<int>();
        using var runner = new PartyAiBatchRunner((_, _) =>
        {
            attempts.Add(visit);
            if (visit == 1 || visit >= 3)
                throw new InvalidOperationException("transient");
        }, logger);

        for (visit = 1; visit <= 4; visit++)
            Visit(runner, party);

        Assert.Equal(new[] { 1, 2, 3, 4 }, attempts);
        Assert.Equal(0, sink.Count(LogEventLevel.Warning));

        // The success on visit 2 reset the count, so visit 5 is the third consecutive failure
        for (; visit <= 6; visit++)
            Visit(runner, party);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, attempts);
        Assert.Equal(1, sink.Count(LogEventLevel.Error));
    }

    [Fact]
    public void TickParties_PersistentFailure_RetriesAfterTenSkippedVisits()
    {
        MobileParty party = CreateParty();
        int visit = 0;
        var attempts = new List<int>();
        using var runner = new PartyAiBatchRunner((_, _) =>
        {
            attempts.Add(visit);
            throw new InvalidOperationException("persistent");
        }, logger);

        for (visit = 1; visit <= 36; visit++)
            Visit(runner, party);

        Assert.Equal(new[] { 1, 2, 3, 14, 25, 36 }, attempts);
        Assert.Equal(1, sink.Count(LogEventLevel.Error));
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
        }, logger);

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
        }, logger);

        for (visit = 1; visit <= 26; visit++)
            Visit(runner, party);

        Assert.Equal(new[] { 1, 2, 3, 14, 25, 26 }, attempts);
        LogEvent recovery = Assert.Single(sink.Events, e => e.Level == LogEventLevel.Information);
        Assert.Equal("Mobile-party AI for \"villager_ES1_1\" recovered after 4 failures", recovery.RenderMessage());
        Assert.Equal(2, sink.Count(LogEventLevel.Warning));
    }

    [Fact]
    public void TickParties_FlappingParty_WarnsOnPowerOfTwoEpisodesAndSecondQuarantine()
    {
        MobileParty party = CreateParty();
        bool broken = false;
        using var runner = new PartyAiBatchRunner((_, _) =>
        {
            if (broken)
                throw new InvalidOperationException("flapping");
        }, logger);

        for (int cycle = 0; cycle < 64; cycle++)
        {
            broken = true;
            for (int visit = 0; visit < 13; visit++)
                Visit(runner, party);

            broken = false;
            Visit(runner, party);
        }

        Assert.Equal(1, sink.Count(LogEventLevel.Error));
        Assert.Equal(7, sink.Count(LogEventLevel.Warning));
        Assert.Equal(7, sink.Count(LogEventLevel.Information));
        Assert.DoesNotContain(sink.Events, e => e.Level < LogEventLevel.Information);

        // Episode 65 is not a power of two, so its first quarantine is quiet but its second is not
        broken = true;
        for (int visit = 0; visit < 13; visit++)
            Visit(runner, party);
        Assert.Equal(7, sink.Count(LogEventLevel.Warning));

        Visit(runner, party);
        Assert.Equal(8, sink.Count(LogEventLevel.Warning));
    }

    [Fact]
    public void TickParties_ExceptionTypeChanges_LogsAnotherErrorAndKeepsCounting()
    {
        MobileParty party = CreateParty();
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
        }, logger);

        for (int visit = 0; visit < 4; visit++)
            Visit(runner, party);

        Assert.Equal(3, attempts);
        Assert.Equal(
            new[] { typeof(InvalidOperationException), typeof(TargetInvocationException) },
            sink.Events.Where(e => e.Level == LogEventLevel.Error).Select(e => e.Exception?.GetType()));
    }

    [Fact]
    public void TickParties_ExceptionMessageThrows_LaterPartiesStillTick()
    {
        MobileParty broken = CreateParty();
        MobileParty healthy = CreateParty();
        int healthyAttempts = 0;
        using var runner = new PartyAiBatchRunner((ai, _) =>
        {
            if (ai == broken.Ai)
                throw new UnreadableMessageException();
            healthyAttempts++;
        }, logger);

        for (int visit = 0; visit < 3; visit++)
            runner.TickParties(new[] { broken, healthy }, 2, 0f);

        Assert.Equal(3, healthyAttempts);
        LogEvent warning = Assert.Single(sink.Events, e => e.Level == LogEventLevel.Warning);
        Assert.Contains(typeof(UnreadableMessageException).FullName!, warning.RenderMessage());
    }

    [Fact]
    public void TickParties_MissingAiThenThrowing_LogsBothErrors()
    {
        MobileParty party = ObjectHelper.SkipConstructor<MobileParty>();
        using var runner = new PartyAiBatchRunner((_, _) => throw new InvalidOperationException("broken"), logger);

        Visit(runner, party);
        party.Ai = new MobilePartyAi(party);
        Visit(runner, party);

        Assert.Equal(
            new object[] { "Party AI is unavailable", "Party AI tick threw" },
            sink.Events.Where(e => e.Level == LogEventLevel.Error).Select(e => ((ScalarValue)e.Properties["Reason"]).Value));
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
        }, logger);

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
        }, logger);

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
        }, logger);

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

    private static MobileParty CreateParty(string? stringId = null)
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.StringId = stringId;
        party.Ai = new MobilePartyAi(party);
        return party;
    }

    private sealed class CaptureSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new List<LogEvent>();

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);

        public int Count(LogEventLevel level) => Events.Count(e => e.Level == level);
    }

    private sealed class UnreadableMessageException : Exception
    {
        public override string Message => throw new InvalidOperationException("message getter");
    }
}
