using Common.Logging;
using GameInterface.Services.MobilePartyAIs.Patches;
using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs;

internal interface IPartyAiBatchRunner
{
    void TickBatch(Campaign campaign, float dt);
}

/// <summary>
/// Ticks server party AI in bounded batches without letting one invalid party stop later parties.
/// A party whose AI keeps throwing is skipped for some of its own visits, then retried.
/// </summary>
internal sealed class PartyAiBatchRunner : IPartyAiBatchRunner, IDisposable
{
    private const int UpdatesPerTick = 100;
    private const int TickDelayMilliseconds = 100;
    private const int FailuresBeforeQuarantine = 3;
    private const int QuarantineVisits = 10;

    private static readonly ILogger DefaultLogger = LogManager.GetLogger<PartyAiBatchRunner>();

    private readonly MobileParty[] batch = new MobileParty[UpdatesPerTick];
    private readonly ConditionalWeakTable<MobileParty, FailureState> failures =
        new ConditionalWeakTable<MobileParty, FailureState>();
    private readonly Action<MobilePartyAi, float> tickOverride;
    private readonly ILogger logger;
    private Task delay = Task.CompletedTask;
    private int currentStartIndex;
    private bool loggedNullParty;

    public PartyAiBatchRunner()
    {
        logger = DefaultLogger;
        PartiesThinkPatch.Bind(this);
    }

    internal PartyAiBatchRunner(Action<MobilePartyAi, float> tickOverride)
        : this(tickOverride, DefaultLogger)
    {
    }

    internal PartyAiBatchRunner(Action<MobilePartyAi, float> tickOverride, ILogger logger)
    {
        if (tickOverride == null) throw new ArgumentNullException(nameof(tickOverride));
        if (logger == null) throw new ArgumentNullException(nameof(logger));
        this.tickOverride = tickOverride;
        this.logger = logger;
    }

    public void TickBatch(Campaign campaign, float dt)
    {
        if (campaign == null || !delay.IsCompleted) return;
        delay = Task.Delay(TickDelayMilliseconds);

        var parties = campaign.MobileParties;
        int partyCount = parties.Count;
        if (partyCount == 0)
        {
            currentStartIndex = 0;
            return;
        }

        int count = Math.Min(UpdatesPerTick, partyCount);
        int startIndex = currentStartIndex % partyCount;
        currentStartIndex = (startIndex + count) % partyCount;

        for (int i = 0; i < count; i++)
            batch[i] = parties[(startIndex + i) % partyCount];

        try
        {
            TickParties(batch, count, dt);
        }
        finally
        {
            Array.Clear(batch, 0, count);
        }
    }

    internal void TickParties(MobileParty[] parties, int count, float dt)
    {
        for (int i = 0; i < count; i++)
        {
            MobileParty party = parties[i];
            if (party == null)
            {
                if (!loggedNullParty)
                {
                    loggedNullParty = true;
                    logger.Error("Skipping null party in mobile-party AI batch");
                }
                continue;
            }

            // Counts the party's own visits, so the skip lasts longer on saves with more parties
            failures.TryGetValue(party, out FailureState state);
            if (state != null && state.SkipVisits > 0)
            {
                state.SkipVisits--;
                continue;
            }

            MobilePartyAi ai = party.Ai;
            if (ai == null)
            {
                LogUnavailableOnce(party, state ?? GetState(party));
                continue;
            }

            try
            {
                if (tickOverride == null)
                    ai.Tick(dt);
                else
                    tickOverride(ai, dt);
            }
            catch (Exception ex)
            {
                RecordFailure(party, state ?? GetState(party), ex);
                continue;
            }

            if (state != null && state.EpisodeFailures > 0)
                RecordRecovery(party, state);
        }
    }

    private FailureState GetState(MobileParty party) =>
        failures.GetValue(party, _ => new FailureState());

    private void LogUnavailableOnce(MobileParty party, FailureState state)
    {
        if (state.UnavailableLogged) return;
        state.UnavailableLogged = true;

        logger.Error(
            "Skipping mobile-party AI tick for {PartyId}: {Reason}",
            party.StringId,
            "Party AI is unavailable");
    }

    private void RecordFailure(MobileParty party, FailureState state, Exception exception)
    {
        // A new exception type gets its own Error so its stack is not hidden behind the first one
        if (state.LoggedExceptionTypes.Add(exception.GetType()))
        {
            logger.Error(
                exception,
                "Skipping mobile-party AI tick for {PartyId}: {Reason}",
                party.StringId,
                "Party AI tick threw");
        }

        if (state.EpisodeFailures < int.MaxValue) state.EpisodeFailures++;
        if (state.EpisodeFailures < FailuresBeforeQuarantine) return;

        // A failed retry quarantines again at once
        state.SkipVisits = QuarantineVisits;
        if (state.EpisodeQuarantines == 0 && state.QuarantinedEpisodes < int.MaxValue) state.QuarantinedEpisodes++;
        if (state.EpisodeQuarantines < int.MaxValue) state.EpisodeQuarantines++;

        // Powers of two keep a flapping or long broken party from filling the log
        bool warn = state.EpisodeQuarantines == 1
            ? IsPowerOfTwo(state.QuarantinedEpisodes)
            : IsPowerOfTwo(state.EpisodeQuarantines);
        if (!warn) return;

        state.EpisodeWarned = true;
        logger.Warning(
            "Skipping mobile-party AI for {PartyId} for its next {Visits} visits after {Failures} failures, last error {Error}",
            party.StringId,
            QuarantineVisits,
            state.EpisodeFailures,
            Describe(exception));
    }

    private void RecordRecovery(MobileParty party, FailureState state)
    {
        if (state.EpisodeWarned)
        {
            logger.Information(
                "Mobile-party AI for {PartyId} recovered after {Failures} failures",
                party.StringId,
                state.EpisodeFailures);
        }

        state.EpisodeFailures = 0;
        state.EpisodeQuarantines = 0;
        state.EpisodeWarned = false;
    }

    private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;

    // Message is virtual and can throw, which must not escape into the batch
    private static string Describe(Exception exception)
    {
        string type = exception.GetType().FullName;
        try
        {
            return type + ": " + exception.Message;
        }
        catch (Exception)
        {
            return type;
        }
    }

    public void Dispose() => PartiesThinkPatch.Unbind(this);

    /// <summary>
    /// Failure bookkeeping for one party, created only once it fails or has no AI.
    /// </summary>
    private sealed class FailureState
    {
        public HashSet<Type> LoggedExceptionTypes { get; } = new HashSet<Type>();
        public bool UnavailableLogged { get; set; }
        public int EpisodeFailures { get; set; }
        public int EpisodeQuarantines { get; set; }
        public int QuarantinedEpisodes { get; set; }
        public bool EpisodeWarned { get; set; }
        public int SkipVisits { get; set; }
    }
}
