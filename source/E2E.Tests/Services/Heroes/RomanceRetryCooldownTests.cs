using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Messages.RomanceFlow;
using GameInterface.Services.Heroes.RomanceFlow;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

/// <summary>
/// v1.5.4 lets a failed courtship start again after one season. The server keeps each player's failure with
/// each courted hero and sends its time to the clients with the romance states (#4588).
/// </summary>
public class RomanceRetryCooldownTests : IDisposable
{
    private const string CourtingControllerId = "RomanceRetryPlayer";

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
        var server = environment.Server;

        server.Call(() =>
        {
            AddRomanceBehavior();
            var courting = server.GetRegisteredObject<Hero>(courtingId);
            var courted = server.GetRegisteredObject<Hero>(courtedId);
            Assert.True(Campaign.Current.Models.MarriageModel.IsCoupleSuitableForMarriage(courting, courted));
            var authority = server.Resolve<IRomanceAuthority>();
            var cooldown = server.Resolve<IRomanceRetryCooldown>();

            Romance.SetRomanticState(courting, courted, Romance.RomanceLevelEnum.FailedInCompatibility);
            Assert.Equal(Romance.RomanceLevelEnum.FailedInCompatibility, Romance.GetRomanticLevel(courting, courted));
            cooldown.RecordFailure(courting, courted);
            Assert.False(cooldown.HasRetryCooldownExpired(courting, courted));

            Assert.False(authority.TryValidateStateChange(
                courting, courted, Romance.RomanceLevelEnum.CourtshipStarted, out var reason));
            Assert.Contains("Cannot change romance", reason);

            Campaign.Current.MapTimeTracker._numTicks += CampaignTime.Days(CampaignTime.DaysInSeason + 1).NumTicks;

            Assert.True(cooldown.HasRetryCooldownExpired(courting, courted));
            Assert.True(authority.TryValidateStateChange(
                courting, courted, Romance.RomanceLevelEnum.CourtshipStarted, out reason), reason);
        });
    }

    [Fact]
    public void Cooldown_IsKeptPerPlayer()
    {
        var (firstId, courtedId) = CreateSuitableCouple();
        var secondId = CreateLord(isFemale: false, age: 30f);
        var server = environment.Server;

        server.Call(() =>
        {
            var first = server.GetRegisteredObject<Hero>(firstId);
            var second = server.GetRegisteredObject<Hero>(secondId);
            var courted = server.GetRegisteredObject<Hero>(courtedId);
            var cooldown = server.Resolve<IRomanceRetryCooldown>();

            cooldown.RecordFailure(first, courted);
            Campaign.Current.MapTimeTracker._numTicks += CampaignTime.Days(CampaignTime.DaysInSeason / 2).NumTicks;
            cooldown.RecordFailure(second, courted);
            Campaign.Current.MapTimeTracker._numTicks += CampaignTime.Days(CampaignTime.DaysInSeason / 2 + 1).NumTicks;

            // The second player's later failure does not hold back the first player's retry.
            Assert.True(cooldown.HasRetryCooldownExpired(first, courted));
            Assert.False(cooldown.HasRetryCooldownExpired(second, courted));

            // The first player's retry clears only the first player's failure.
            cooldown.Clear(first, courted);
            Assert.True(cooldown.FailureTicks(second, courted) > 1);
            Assert.False(cooldown.HasRetryCooldownExpired(second, courted));
        });
    }

    [Fact]
    public void FailedCourtshipWithoutARecord_CanBeRetried()
    {
        var (courtingId, courtedId) = CreateSuitableCouple();
        var server = environment.Server;

        server.Call(() =>
        {
            var courting = server.GetRegisteredObject<Hero>(courtingId);
            var courted = server.GetRegisteredObject<Hero>(courtedId);
            var cooldown = server.Resolve<IRomanceRetryCooldown>();

            // A save from before the history has the failed level but no failure time.
            Assert.True(cooldown.HasRetryCooldownExpired(courting, courted));
            Assert.Equal(1, cooldown.FailureTicks(courting, courted));
        });
    }

    [Fact]
    public void Snapshot_PutsThePlayersOwnFailureIntoTheClientsAttemptList()
    {
        var client = environment.Clients.First();
        var (courtingId, courtedId) = CreateSuitableCouple();
        long serverTicks = 0;

        RegisterCourtingPlayer(environment.Server, courtingId, isClient: false);
        RegisterCourtingPlayer(client, courtingId, isClient: true);
        environment.Server.Call(() =>
        {
            AddRomanceBehavior();
            var courting = environment.Server.GetRegisteredObject<Hero>(courtingId);
            var courted = environment.Server.GetRegisteredObject<Hero>(courtedId);
            var cooldown = environment.Server.Resolve<IRomanceRetryCooldown>();
            Romance.SetRomanticState(courting, courted, Romance.RomanceLevelEnum.FailedInPracticalities);
            cooldown.RecordFailure(courting, courted);
            serverTicks = cooldown.FailureTicks(courting, courted);
        });
        environment.FlushCoalescer();
        Assert.True(serverTicks > 1);

        client.Call(() => AddRomanceBehavior());
        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestRomanceStateSync()));
        environment.FlushCoalescer();
        environment.Server.PumpGameThread();
        client.PumpGameThread();

        client.Call(() =>
        {
            var courting = client.GetRegisteredObject<Hero>(courtingId);
            var courted = client.GetRegisteredObject<Hero>(courtedId);
            Assert.Same(courting, Hero.MainHero);
            Assert.Equal(
                Romance.RomanceLevelEnum.FailedInPracticalities,
                Romance.GetRomanticLevel(courting, courted));

            var attempts = Campaign.Current.GetCampaignBehavior<RomanceCampaignBehavior>()
                ._previousRomancePersuasionAttempts;
            var failure = Assert.Single(attempts);
            Assert.Same(courted, failure.PersuadedHero);
            Assert.False(failure.IsSuccesful());
            Assert.Equal(serverTicks, failure.GameTime.NumTicks);
        });
    }

    private static void AddRomanceBehavior() =>
        Campaign.Current.AddCampaignBehaviorManager(
            new CampaignBehaviorManager(new CampaignBehaviorBase[] { new RomanceCampaignBehavior() }));

    // As WorkshopPurchaseConversationTests: the player list makes the hero a player hero, and on the client the
    // player troop makes it Hero.MainHero.
    private static void RegisterCourtingPlayer(EnvironmentInstance instance, string heroId, bool isClient)
    {
        instance.Call(() =>
        {
            if (isClient) instance.Resolve<IControllerIdProvider>().SetControllerId(CourtingControllerId);
            var hero = instance.GetRegisteredObject<Hero>(heroId);
            Assert.True(instance.ObjectManager.TryGetId(hero.Clan, out var clanId));
            Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(
                new Player(CourtingControllerId, heroId, string.Empty, clanId, string.Empty)));

            if (!isClient) return;
            using (new AllowedThread())
            {
                Game.Current.PlayerTroop = hero.CharacterObject;
                Campaign.Current.PlayerDefaultFaction = hero.Clan;
            }
        });
    }

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
