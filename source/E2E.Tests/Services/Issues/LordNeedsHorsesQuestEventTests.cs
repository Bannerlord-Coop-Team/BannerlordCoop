using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssueQuest;

namespace E2E.Tests.Services.Issues;

public class LordNeedsHorsesQuestEventTests : IDisposable
{
    private const string ControllerA = "player-A";
    private const string IssueId = "horse_events";
    private const int Mounts = 6;
    private const int Requested = 4;

    private static readonly MethodBase[] LogTexts =
    {
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.OnQuestCancelled3LogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.OnQuestCancelled2LogText)),
    };

    private readonly StubIssuePreconditions preconditions = new(typeof(Issue));
    private readonly string giverId;
    private readonly string captorPartyId;
    private readonly Player player;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance ClientA => TestEnvironment.Clients.First();
    private EnvironmentInstance ClientB => TestEnvironment.Clients.Last();
    private EnvironmentInstance[] Instances => new[] { Server, ClientA, ClientB };

    public LordNeedsHorsesQuestEventTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        giverId = TestEnvironment.CreateRegisteredObject<Hero>();
        captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        player = new Player(ControllerA, TestEnvironment.CreateRegisteredObject<Hero>(), TestEnvironment.CreateRegisteredObject<MobileParty>(), "", "");

        foreach (var instance in Instances)
        {
            instance.Call(() => Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(player)));
            AddIssue(instance);
        }

        TestEnvironment.ConnectRegisteredPlayer(ClientA, ControllerA);
        ClientA.Resolve<IControllerIdProvider>().SetControllerId(ControllerA);
        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
            Game.Current.PlayerTroop = hero.CharacterObject;
        });
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
        preconditions.Dispose();
    }

    [Fact]
    public void TheMountsLeaveTheOwnerOnlyOnTheServer()
    {
        var mount = DefaultItems.Grain;

        foreach (var instance in new[] { Server, ClientA })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var ownerParty));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var giverParty));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(player.HeroId, out var owner));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));

                using (new AllowedThread())
                {
                    owner._partyBelongedTo = ownerParty;
                    giver._partyBelongedTo = giverParty;
                    ownerParty.ItemRoster.Clear();
                    giverParty.ItemRoster.Clear();
                    ownerParty.ItemRoster.AddToCounts(mount, Mounts);
                }

                var quest = (Quest)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Quest));
                quest.GiveMounts(owner, giver, ownerParty.ItemRoster.GetElementCopyAtIndex(0), Requested);

                var expectedOwner = instance == Server ? Mounts - Requested : Mounts;
                var expectedGiver = instance == Server ? Requested : 0;
                Assert.Equal(expectedOwner, ownerParty.ItemRoster.GetItemNumber(mount));
                Assert.Equal(expectedGiver, giverParty.ItemRoster.GetItemNumber(mount));
            });
        }
    }

    [Fact]
    public void TheStartGoldComesOffOnlyOnTheServer()
    {
        const int Gold = 100000;

        foreach (var instance in new[] { Server, ClientA })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(player.HeroId, out var owner));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var ownerParty));

                using (new AllowedThread())
                {
                    owner._partyBelongedTo = ownerParty;
                    owner.Gold = Gold;
                }

                var issue = Uninitialized<Issue>();
                AccessTools.Field(typeof(Issue), nameof(Issue._mountValuePerUnit)).SetValue(issue, 10);
                issue._numMountsToBeDelivered = Requested;

                using (new MainHeroSubstitutionScope(owner, ownerParty))
                {
                    issue.AlternativeSolutionStartConsequence();
                }

                Assert.Equal(instance == Server ? Gold - Requested * 10 : Gold, owner.Gold);
            });
        }
    }

    [Fact]
    public void ACapturedGiverEndsTheOwnersQuestEverywhere()
    {
        StartQuestOnClientA();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var captor));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));

            CampaignEventDispatcher.Instance.OnHeroPrisonerTaken(captor.Party, giver);
        }, LogTexts);

        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestCancel, finalized.Outcome);

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.Null(giver.Issue);
            });
        }
    }

    [Fact]
    public void ACaptureSeenOnTheOwnersClientLeavesTheQuestToTheServer()
    {
        StartQuestOnClientA();

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var captor));
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(giverId, out var giver));

            using (new AllowedThread())
            {
                CampaignEventDispatcher.Instance.OnHeroPrisonerTaken(captor.Party, giver);
            }

            Assert.NotNull(giver.Issue);
            Assert.True(giver.Issue.IssueQuest.IsOngoing);
        }, LogTexts);

        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
    }

    [Fact]
    public void AKingdomChangeIsHandedToTheOwnersQuestOnTheServer()
    {
        StartQuestOnClientA();
        using var calls = new MethodCallRecorder(AccessTools.Method(typeof(Quest), nameof(Quest.OnClanChangedKingdom)));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var owner));

            CampaignEventDispatcher.Instance.OnClanChangedKingdom(owner.Clan, null, null, TaleWorlds.CampaignSystem.Actions.ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom, false);
        }, LogTexts);

        Assert.Equal(1, calls.CountFor(Server));
    }

    [Fact]
    public void AWarIsHandedToTheOwnersQuestOnTheServer()
    {
        StartQuestOnClientA();
        using var calls = new MethodCallRecorder(AccessTools.Method(typeof(Quest), nameof(Quest.OnWarDeclared)));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));

            CampaignEventDispatcher.Instance.OnWarDeclared(giver.MapFaction, giver.MapFaction, TaleWorlds.CampaignSystem.Actions.DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility);
        }, LogTexts);

        Assert.Equal(1, calls.CountFor(Server));
    }

    private void StartQuestOnClientA()
    {
        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            giver.Issue.StartIssueWithQuest();
        });
    }

    private void AddIssue(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(player.HeroId, out var owner));
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var ownerParty));

            using (new AllowedThread())
            {
                // The quest's dialogs read encyclopedia links
                Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

                owner._partyBelongedTo = ownerParty;

                var issue = new Issue(giver);
                issue.StringId = IssueId;
                issue._issueState = IssueBase.IssueState.Ongoing;

                Campaign.Current.IssueManager._issues.Add(giver, issue);
                giver.OnIssueCreatedForHero(issue);
            }
        });
    }

    private static T Uninitialized<T>() where T : class => (T)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(T));
}
