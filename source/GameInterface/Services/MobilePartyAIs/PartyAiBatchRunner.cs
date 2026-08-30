using Common.Logging;
using GameInterface.Services.MobilePartyAIs.Patches;
using Serilog;
using System;
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
/// </summary>
internal sealed class PartyAiBatchRunner : IPartyAiBatchRunner, IDisposable
{
    private const int UpdatesPerTick = 100;
    private const int TickDelayMilliseconds = 100;

    private static readonly ILogger Logger = LogManager.GetLogger<PartyAiBatchRunner>();

    private readonly MobileParty[] batch = new MobileParty[UpdatesPerTick];
    private readonly ConditionalWeakTable<MobileParty, object> loggedFailures =
        new ConditionalWeakTable<MobileParty, object>();
    private readonly Action<MobilePartyAi, float> tickOverride;
    private Task delay = Task.CompletedTask;
    private int currentStartIndex;
    private bool loggedNullParty;

    public PartyAiBatchRunner()
    {
        PartiesThinkPatch.Bind(this);
    }

    internal PartyAiBatchRunner(Action<MobilePartyAi, float> tickOverride)
    {
        if (tickOverride == null) throw new ArgumentNullException(nameof(tickOverride));
        this.tickOverride = tickOverride;
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
                    Logger.Error("Skipping null party in mobile-party AI batch");
                }
                continue;
            }

            MobilePartyAi ai = party.Ai;
            if (ai == null)
            {
                LogFailureOnce(party, null, "Party AI is unavailable");
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
                LogFailureOnce(party, ex, "Party AI tick threw");
            }
        }
    }

    private void LogFailureOnce(MobileParty party, Exception exception, string reason)
    {
        if (loggedFailures.TryGetValue(party, out _)) return;
        loggedFailures.Add(party, new object());

        if (exception == null)
        {
            Logger.Error(
                "Skipping mobile-party AI tick for {PartyId}: {Reason}",
                party.StringId,
                reason);
        }
        else
        {
            Logger.Error(
                exception,
                "Skipping mobile-party AI tick for {PartyId}: {Reason}",
                party.StringId,
                reason);
        }
    }

    public void Dispose() => PartiesThinkPatch.Unbind(this);
}
