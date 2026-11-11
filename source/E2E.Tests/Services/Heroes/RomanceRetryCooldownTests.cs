using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.Messages.RomanceFlow;
using GameInterface.Services.Heroes.RomanceFlow;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

/// <summary>
/// v1.5.4 lets a failed courtship start again after one season. The server keeps the failure in vanilla's
/// persuasion attempt list and sends its time to the clients with the romance states.
/// </summary>
public class RomanceRetryCooldownTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private readonly Harmony clock = new($"e2e.romance-retry.{Guid.NewGuid():N}");

    // The test bootstrap pins CampaignTime.Now, so read the map time tracker as HideoutRaidEntryTests does,
    // and start one year in: a failure at tick 0 reads as no failure.
    public RomanceRetryCooldownTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        clock.Patch(AccessTools.PropertyGetter(typeof(CampaignTime), nameof(CampaignTime.Now)),
            postfix: new HarmonyMethod(typeof(RomanceRetryCooldownTests), nameof(ProvideCampaignTime)));
        foreach (var instance in environment.Clients.Append(environment.Server))
            instance.Call(() => Campaign.Current.MapTimeTracker._numTicks = CampaignTime.Days(CampaignTime.DaysInYear).NumTicks);
    }

    public void Dispose()
    {
        clock.UnpatchAll(clock.Id);
        environment.Dispose();
    }

    private static void ProvideCampaignTime(ref CampaignTime __result) => __result = Campaign.Current.MapTimeTracker.Now;

    [Fact]
    public void FailedCourtship_RetryIsRefusedWithinASeasonAndAllowedAfterIt()
    {
        var (courtingId, courtedId) = CreateSuitableCouple();

        environment.Server.Call(() =>
        {
            AddRomanceBehavior();
            var courting = environment.Server.GetRegisteredObject<Hero>(courtingId);
            var courted = environment.Server.GetRegisteredObject<Hero>(courtedId);
            Assert.True(Campaign.Current.Models.MarriageModel.IsCoupleSuitableForMarriage(courting, courted));
            var authority = new RomanceAuthority();

            Romance.SetRomanticState(courting, courted, Romance.RomanceLevelEnum.FailedInCompatibility);
            Assert.Equal(Romance.RomanceLevelEnum.FailedInCompatibility, Romance.GetRomanticLevel(courting, courted));
            RomanceRetryCooldown.RecordFailure(courted);
            Assert.False(RomanceRetryCooldown.HasRetryCooldownExpired(courted));

            Assert.False(authority.TryValidateStateChange(
                courting, courted, Romance.RomanceLevelEnum.CourtshipStarted, out var reason));
            Assert.Contains("Cannot change romance", reason);

            Campaign.Current.MapTimeTracker._numTicks += CampaignTime.Days(CampaignTime.DaysInSeason + 1).NumTicks;

            Assert.True(RomanceRetryCooldown.HasRetryCooldownExpired(courted));
            Assert.True(authority.TryValidateStateChange(
                courting, courted, Romance.RomanceLevelEnum.CourtshipStarted, out reason), reason);
        });
    }

    [Fact]
    public void Cooldown_WithNoAttempt_HasNotExpired_AsInVanilla()
    {
        var (_, courtedId) = CreateSuitableCouple();

        environment.Server.Call(() =>
        {
            AddRomanceBehavior();
            var courted = environment.Server.GetRegisteredObject<Hero>(courtedId);

            Assert.False(RomanceRetryCooldown.HasRetryCooldownExpired(courted));

            RomanceRetryCooldown.RecordFailure(courted);
            RomanceRetryCooldown.Clear(courted);

            Assert.Equal(0, RomanceRetryCooldown.LatestFailureTicks(courted));
            Assert.False(RomanceRetryCooldown.HasRetryCooldownExpired(courted));
        });
    }

    [Fact]
    public void Snapshot_CarriesTheFailureTimeToTheClientsAttemptList()
    {
        var client = environment.Clients.First();
        var (courtingId, courtedId) = CreateSuitableCouple();
        long serverTicks = 0;

        environment.Server.Call(() =>
        {
            AddRomanceBehavior();
            var courting = environment.Server.GetRegisteredObject<Hero>(courtingId);
            var courted = environment.Server.GetRegisteredObject<Hero>(courtedId);
            Romance.SetRomanticState(courting, courted, Romance.RomanceLevelEnum.FailedInPracticalities);
            // Neither hero is a player here, so record the failure for both: the snapshot reads Person1's.
            RomanceRetryCooldown.RecordFailure(courting);
            RomanceRetryCooldown.RecordFailure(courted);
            serverTicks = RomanceRetryCooldown.LatestFailureTicks(courted);
        });
        environment.FlushCoalescer();
        Assert.True(serverTicks > 0);

        client.Call(() => AddRomanceBehavior());
        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestRomanceStateSync()));
        environment.FlushCoalescer();
        environment.Server.PumpGameThread();
        client.PumpGameThread();

        client.Call(() =>
        {
            var courting = client.GetRegisteredObject<Hero>(courtingId);
            var courted = client.GetRegisteredObject<Hero>(courtedId);
            Assert.Equal(
                Romance.RomanceLevelEnum.FailedInPracticalities,
                Romance.GetRomanticLevel(courting, courted));

            var attempts = Campaign.Current.GetCampaignBehavior<RomanceCampaignBehavior>()
                ._previousRomancePersuasionAttempts;
            var failure = Assert.Single(attempts);
            Assert.Contains(failure.PersuadedHero, new[] { courting, courted });
            Assert.False(failure.IsSuccesful());
            Assert.Equal(serverTicks, failure.GameTime.NumTicks);
        });
    }

    private static void AddRomanceBehavior() =>
        Campaign.Current.AddCampaignBehaviorManager(
            new CampaignBehaviorManager(new CampaignBehaviorBase[] { new RomanceCampaignBehavior() }));

    // Two adult lords of opposite sex in different clans, which the real marriage model accepts.
    private (string CourtingId, string CourtedId) CreateSuitableCouple()
    {
        var courtingId = CreateLord(isFemale: false, age: 28f);
        var courtedId = CreateLord(isFemale: true, age: 24f);
        return (courtingId, courtedId);
    }

    private string CreateLord(bool isFemale, float age)
    {
        var heroId = environment.CreateRegisteredObject<Hero>();
        environment.Server.Call(() =>
        {
            var hero = environment.Server.GetRegisteredObject<Hero>(heroId);
            hero._heroState = Hero.CharacterStates.Active;
            hero.SetBirthDay(CampaignTime.YearsFromNow(-age));
            hero.Occupation = Occupation.Lord;
            hero.IsFemale = isFemale;
            hero.Clan = Clan.CreateClan($"RomanceRetryClan{heroId}");
        });
        return heroId;
    }
}
