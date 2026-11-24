using Common.Util;
using GameInterface.Services.BattleWreckages;
using GameInterface.Services.BattleWreckages.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BattleWreckages;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using Xunit.Abstractions;

namespace E2E.Tests.Services.BattleWreckages;

/// <summary>
/// The interrupt and expiry time rules the server keeps per player wreckage encounter. CampaignTime's hour length
/// comes from a running campaign, so these run in the E2E environment, with explicit times and no clock patch.
/// </summary>
public class BattleWreckageEncounterTrackerTests : BattleWreckageTestBase
{
    // Non-zero, because a zero LastInteractionTime means the encounter was never interrupted
    private static readonly CampaignTime StartTime = new CampaignTime(1_000_000_000L);

    private readonly BattleWreckageEncounterTracker tracker = new BattleWreckageEncounterTracker();

    public BattleWreckageEncounterTrackerTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Interrupt_KeepsTheTimeLeftAndRecordsTheInterrupt()
    {
        Server.Call(() =>
        {
            var encounter = CreateRunningEncounter(requiredHours: 10);
            var now = StartTime + CampaignTime.Hours(4);

            tracker.Interrupt(encounter, now);

            Assert.Equal(6, encounter.RequiredInvestigationDuration.ToHours, 3);
            Assert.Equal(now, encounter.LastInteractionTime);
            Assert.False(encounter.IsInvestigating);
            Assert.Equal(CampaignTime.Zero, encounter.InvestigationStartTime);
        });
    }

    // Vanilla always leaves at least an hour to resume, even when the investigation already ran its full time
    [Fact]
    public void Interrupt_PastTheRequiredTime_LeavesOneHour()
    {
        Server.Call(() =>
        {
            var encounter = CreateRunningEncounter(requiredHours: 3);

            tracker.Interrupt(encounter, StartTime + CampaignTime.Hours(5));

            Assert.Equal(1, encounter.RequiredInvestigationDuration.ToHours, 3);
        });
    }

    [Fact]
    public void GetExpiredEncounters_ReturnsOnlyInterruptsOlderThanTheExpiryWindow()
    {
        Server.Call(() =>
        {
            var expired = CreateRunningEncounter(requiredHours: 10);
            var recent = CreateRunningEncounter(requiredHours: 10);
            var running = CreateRunningEncounter(requiredHours: 10);
            tracker.AddEncounter(expired);
            tracker.AddEncounter(recent);
            tracker.AddEncounter(running);

            tracker.Interrupt(expired, StartTime);
            tracker.Interrupt(recent, StartTime + CampaignTime.Hours(2));
            var now = StartTime + CampaignTime.Hours(BattleWreckageCampaignBehavior.InterruptedSnapshotExpiryAsHours + 1);

            Assert.Same(expired, Assert.Single(tracker.GetExpiredEncounters(now)));
        });
    }

    private static BattleWreckageEncounter CreateRunningEncounter(float requiredHours)
    {
        return new BattleWreckageEncounter(ObjectHelper.SkipConstructor<Hero>(), ObjectHelper.SkipConstructor<BattleWreckage>())
        {
            IsInvestigating = true,
            InvestigationStartTime = StartTime,
            RequiredInvestigationDuration = CampaignTime.Hours(requiredHours),
        };
    }
}
