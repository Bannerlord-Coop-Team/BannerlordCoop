using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// The world around two gang leaders in two towns and two connected players, accepted through the framework's accept
/// like every other quest.
/// </summary>
public abstract class GangLeaderNeedsWeaponsAcceptScenario : IDisposable
{
    protected const string ControllerA = "player-A";
    protected const string ControllerB = "player-B";
    protected const string FirstIssueId = "weapons_first";
    protected const string SecondIssueId = "weapons_second";

    private readonly string townClanId;
    private readonly Player playerA;
    private readonly Player playerB;

    protected string FirstGiverId { get; }
    protected string SecondGiverId { get; }

    protected E2ETestEnvironment TestEnvironment { get; }
    protected EnvironmentInstance Server => TestEnvironment.Server;
    protected EnvironmentInstance ClientA => TestEnvironment.Clients.First();
    protected EnvironmentInstance ClientB => TestEnvironment.Clients.Last();
    protected EnvironmentInstance[] Instances => new[] { Server, ClientA, ClientB };
    protected Player PlayerA => playerA;

    protected GangLeaderNeedsWeaponsAcceptScenario(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        townClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        FirstGiverId = TestEnvironment.CreateRegisteredObject<Hero>();
        SecondGiverId = TestEnvironment.CreateRegisteredObject<Hero>();

        playerA = CreatePlayer(ControllerA);
        playerB = CreatePlayer(ControllerB);

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(playerA));
                Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(playerB));
            });

            AddIssue(instance, FirstGiverId, FirstIssueId);
            AddIssue(instance, SecondGiverId, SecondIssueId);
        }

        Connect(ClientA, playerA);
        Connect(ClientB, playerB);
    }

    public virtual void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private Player CreatePlayer(string controllerId)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        return new Player(controllerId, heroId, partyId, "", "");
    }

    private void Connect(EnvironmentInstance client, Player player)
    {
        TestEnvironment.ConnectRegisteredPlayer(client, player.ControllerId);
        client.Resolve<IControllerIdProvider>().SetControllerId(player.ControllerId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
            Game.Current.PlayerTroop = hero.CharacterObject;
            Assert.Same(hero, Hero.MainHero);
        });
    }

    private void AddIssue(EnvironmentInstance instance, string giverId, string issueId)
    {
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();

        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(instance.ObjectManager.TryGetObject<Town>(townId, out var town));
            Assert.True(instance.ObjectManager.TryGetObject<Clan>(townClanId, out var townClan));

            using (new AllowedThread())
            {
                // The quest's dialogs read encyclopedia links
                Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

                // A town the gang leader can stay alive in
                settlement.SetSettlementComponent(town);
                town.OwnerClan = townClan;
                town.Loyalty = 50;
                giver.StayingInSettlement = settlement;
                giver.Occupation = Occupation.GangLeader;

                foreach (var player in new[] { playerA, playerB })
                {
                    Assert.True(instance.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
                    Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
                    hero._partyBelongedTo = party;
                }

                var issue = new Issue(giver);
                issue._averagePriceForItem = 250;
                issue._requiredWeaponClassIndex = 0;
                issue.StringId = issueId;
                issue._issueState = IssueBase.IssueState.Ongoing;

                Campaign.Current.IssueManager._issues.Add(giver, issue);
                giver.OnIssueCreatedForHero(issue);
            }
        });
    }

    protected static void Accept(EnvironmentInstance client, string giverId)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            giver.Issue.StartIssueWithQuest();
        });
    }

    protected static bool CheckPreconditions(EnvironmentInstance client, string giverId)
    {
        var result = false;

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            result = giver.Issue.CheckPreconditions(giver, out _);
        });

        return result;
    }

    protected string? OwnerOnServer(string giverId, string issueId)
    {
        string? controllerId = null;

        Server.Call(() =>
        {
            Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(giverId, issueId, out controllerId);
        });

        return controllerId;
    }
}

/// <summary>
/// Only the world requirements of taking the quest are stubbed (relations, the town's owner), the rules under test are real.
/// </summary>
public class GangLeaderNeedsWeaponsAcceptTests : GangLeaderNeedsWeaponsAcceptScenario
{
    private readonly StubIssuePreconditions preconditions = new(typeof(Issue));

    public GangLeaderNeedsWeaponsAcceptTests(ITestOutputHelper output) : base(output)
    {
    }

    public override void Dispose()
    {
        base.Dispose();
        preconditions.Dispose();
    }

    [Fact]
    public void EachPlayer_CanHoldAWeaponsQuestAtTheSameTime()
    {
        Accept(ClientA, FirstGiverId);
        Accept(ClientB, SecondGiverId);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.Equal(ControllerA, OwnerOnServer(FirstGiverId, FirstIssueId));
        Assert.Equal(ControllerB, OwnerOnServer(SecondGiverId, SecondIssueId));
    }

    [Fact]
    public void ThePlayerHoldingAWeaponsQuest_IsRefusedASecondOne()
    {
        Accept(ClientA, FirstGiverId);
        Accept(ClientA, SecondGiverId);

        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.Equal(SecondIssueId, rejected.IssueId);
        Assert.Null(OwnerOnServer(SecondGiverId, SecondIssueId));
        Assert.Equal(ControllerA, OwnerOnServer(FirstGiverId, FirstIssueId));
    }

    [Fact]
    public void ThePlayerHoldingAWeaponsQuest_IsRefusedASecondOneEvenWhenAnotherPlayerHoldsOneToo()
    {
        Accept(ClientA, FirstGiverId);
        Accept(ClientB, SecondGiverId);

        Assert.False(CheckPreconditions(ClientA, SecondGiverId));
        Assert.False(CheckPreconditions(ClientB, FirstGiverId));
    }

    [Fact]
    public void AnotherPlayersWeaponsQuest_DoesNotCountAgainstAPlayersOwnCheck()
    {
        Accept(ClientA, FirstGiverId);

        Assert.True(CheckPreconditions(ClientB, SecondGiverId));
    }
}

/// <summary>
/// Nothing is stubbed: a player who can legitimately take the quest or send a companion passes the server's accept check.
/// </summary>
public class GangLeaderNeedsWeaponsEligibilityTests : GangLeaderNeedsWeaponsAcceptScenario
{
    private const int SentTroops = 8;
    private readonly StubIssuePreconditions preconditions = new(typeof(Issue));

    public GangLeaderNeedsWeaponsEligibilityTests(ITestOutputHelper output) : base(output)
    {
    }

    public override void Dispose()
    {
        base.Dispose();
        preconditions.Dispose();
    }

    [Fact]
    public void AnEligiblePlayer_IsNotRefusedTheQuest()
    {
        Accept(ClientA, FirstGiverId);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
        Assert.Equal(ControllerA, accepted.ControllerId);
        Assert.Equal(ControllerA, OwnerOnServer(FirstGiverId, FirstIssueId));
    }

    [Fact]
    public void AnEligiblePlayer_IsNotRefusedTheCompanionMission()
    {
        var (startingGold, fee) = AcceptTheCompanionMission();

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAcceptRejected>());
        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAccepted>());
        Assert.Equal(ControllerA, accepted.ControllerId);
        Assert.Equal(ControllerA, OwnerOnServer(FirstGiverId, FirstIssueId));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(PlayerA.HeroId, out var owner));
            Assert.Equal(startingGold - fee, owner.Gold);
        });
    }

    [Fact]
    public void ARejectedCompanionMissionDoesNotChargeTheOwner()
    {
        preconditions.Refuse(Server);
        var (startingGold, _) = AcceptTheCompanionMission();

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAcceptRejected>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(PlayerA.HeroId, out var owner));
            Assert.Equal(startingGold, owner.Gold);
        });
    }

    [Fact]
    public void AForgedSuccessRequestDoesNotCompleteTheQuest()
    {
        Accept(ClientA, FirstGiverId);

        ClientA.Call(() => ClientA.Resolve<INetwork>().SendAll(new RequestQuestOutcome(
            FirstGiverId,
            FirstIssueId,
            IssueOutcome.QuestSuccess)));
        TestEnvironment.FlushCoalescer();

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(FirstGiverId, out var giver));
            Assert.NotNull(giver.Issue);
            Assert.NotNull(giver.Issue.IssueQuest);
        });
    }

    [Fact]
    public void TheDeliveryBranchIsRejectedAwayFromTheQuestGiversSettlement()
    {
        var axe = GangLeaderNeedsWeaponsTests.CreateAxe();

        Accept(ClientA, FirstGiverId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(FirstGiverId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(PlayerA.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PlayerA.MobilePartyId, out var party));
            var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);

            using (new AllowedThread())
            {
                owner._partyBelongedTo = party;
                party.CurrentSettlement = null;
                party.ItemRoster.Clear();
                party.ItemRoster.AddToCounts(axe, quest._requestedWeaponAmount);
            }
        });

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(FirstGiverId, out var giver));
            Assert.IsType<Quest>(giver.Issue.IssueQuest).PlayerSuccessfullyDeliveredWeapons();
        });
        TestEnvironment.FlushCoalescer();

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(FirstGiverId, out var giver));
            Assert.NotNull(giver.Issue);
        });
    }

    [Fact]
    public void AClientThatDidNotStartTheMission_TicksItsDayWithOneQuietLogEntry()
    {
        AcceptTheCompanionMission();

        ClientB.Call(() =>
        {
            Assert.True(ClientB.ObjectManager.TryGetObject<Hero>(FirstGiverId, out var giver));
            Assert.True(giver.Issue.IsSolvingWithAlternative);

            // Vanilla's daily tick reads the first entry of every running mission
            Campaign.Current.IssueManager.DailyTick();
            var entry = Assert.Single(giver.Issue.JournalEntries);
            var range = ((Issue)giver.Issue).AlternativeSolutionBaseDurationInDaysInternal;
            Assert.Equal(range, entry.Range);
            Assert.Equal(Math.Min(1, range), entry.CurrentProgress);

            Campaign.Current.IssueManager.DailyTick();
            Assert.Same(entry, Assert.Single(giver.Issue.JournalEntries));
            Assert.Equal(Math.Min(2, range), entry.CurrentProgress);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheHandIn_PaysTheOwnerAndTakesTheWeaponsOnlyWhenTheyAreCarried(bool carriesEnough)
    {
        var axe = GangLeaderNeedsWeaponsTests.CreateAxe();
        var needed = 0;
        var rewardGold = 0;
        var goldBefore = 0;
        var powerBefore = 0f;

        Accept(ClientA, FirstGiverId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(FirstGiverId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(PlayerA.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PlayerA.MobilePartyId, out var party));
            var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);

            needed = quest._requestedWeaponAmount;
            rewardGold = quest.RewardGold;
            goldBefore = owner.Gold;
            powerBefore = giver.Power;

            using (new AllowedThread())
            {
                party.ItemRoster.Clear();
                party.CurrentSettlement = carriesEnough ? giver.CurrentSettlement : null;
                party.ItemRoster.AddToCounts(axe, carriesEnough ? needed : needed - 1);
            }
        });
        TestEnvironment.FlushCoalescer();

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(FirstGiverId, out var giver));
            Assert.IsType<Quest>(giver.Issue.IssueQuest).PlayerSuccessfullyDeliveredWeapons();
        });
        TestEnvironment.FlushCoalescer();

        var finished = Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(FirstGiverId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(PlayerA.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PlayerA.MobilePartyId, out var party));

            if (carriesEnough)
            {
                Assert.Equal(goldBefore + rewardGold, owner.Gold);
                Assert.Equal(0, party.ItemRoster.GetItemNumber(axe));
                Assert.Null(giver.Issue);
                Assert.Single(finished, message => message.Outcome == IssueOutcome.QuestSuccess);
                Assert.Equal(powerBefore + 10f, giver.Power);
            }
            else
            {
                Assert.Equal(goldBefore, owner.Gold);
                Assert.Equal(needed - 1, party.ItemRoster.GetItemNumber(axe));
                Assert.NotNull(giver.Issue.IssueQuest);
                Assert.Empty(finished);
                Assert.Equal(powerBefore, giver.Power);
            }
        });

        // The giver's power is a hero field, so the clients must end up with the server's value as well
        foreach (var client in new[] { ClientA, ClientB })
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(FirstGiverId, out var giver));
                Assert.Equal(carriesEnough ? powerBefore + 10f : powerBefore, giver.Power);
            });
        }
    }

    private (int StartingGold, int Fee) AcceptTheCompanionMission()
    {
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var startingGold = 0;
        var fee = 0;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(PlayerA.HeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PlayerA.MobilePartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));

            using (new AllowedThread())
            {
                troop.Level = 20;
                playerHero.Gold = 100000;
                startingGold = playerHero.Gold;
                party.MemberRoster.AddToCounts(playerHero.CharacterObject, 1);
                party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                party.MemberRoster.AddToCounts(troop, SentTroops);
            }

            Assert.True(Server.ObjectManager.TryGetObject<Hero>(FirstGiverId, out var giver));
            fee = (int)AccessTools.Property(typeof(Issue), "CompanionGoldNeedForAlternativeSolution").GetValue(giver.Issue)!;
        });
        TestEnvironment.FlushCoalescer();

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(FirstGiverId, out var giver));
            Assert.True(ClientA.ObjectManager.TryGetObject<MobileParty>(PlayerA.MobilePartyId, out var party));
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(ClientA.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));

            using (new AllowedThread())
            {
                Campaign.Current.MainParty = party;
                giver.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                giver.Issue.AlternativeSolutionSentTroops.AddToCounts(troop, SentTroops);
            }

            giver.Issue.StartIssueWithAlternativeSolution();
        });
        TestEnvironment.FlushCoalescer();

        return (startingGold, fee);
    }
}
