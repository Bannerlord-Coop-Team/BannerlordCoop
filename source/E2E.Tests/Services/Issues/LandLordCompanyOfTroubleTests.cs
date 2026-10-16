using Autofac;
using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Helpers;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace E2E.Tests.Services.Issues;

public class LandLordCompanyOfTroubleTests : IDisposable
{
    private const string IssueName = "LandLordCompanyOfTroubleIssue";
    private const string IssueId = "issue_company_of_trouble";
    private const string OwnerControllerId = "player-A";

    private static readonly MethodBase[] CharacterLinks = AccessTools.GetDeclaredMethods(typeof(StringHelpers))
        .Where(method => method.Name == nameof(StringHelpers.SetCharacterProperties))
        .ToArray<MethodBase>();

    private readonly MethodCallRecorder questEndings;
    private readonly string lordId;
    private readonly string troopId;
    private readonly string ownerHeroId;
    private readonly string ownerPartyId;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Owner => TestEnvironment.Clients.First();

    public LandLordCompanyOfTroubleTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        questEndings = new MethodCallRecorder(
            AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithSuccess)),
            AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithFail)));

        lordId = TestEnvironment.CreateRegisteredObject<Hero>();
        troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        ownerHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        ownerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(OwnerControllerId, ownerHeroId, ownerPartyId, "", "")));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));

            using (new AllowedThread())
            {
                ownerHero._partyBelongedTo = ownerParty;
            }
        });
        TestEnvironment.ConnectRegisteredPlayer(Owner, OwnerControllerId);
        Owner.Resolve<IControllerIdProvider>().SetControllerId(OwnerControllerId);

        foreach (var instance in new[] { Server, Owner })
        {
            GiveTheLordTheIssue(instance);
        }
    }

    public void Dispose()
    {
        questEndings.Dispose();
        TestEnvironment.Dispose();
    }

    [Fact]
    public void DebugCapture_IsRefusedForAHeroWithoutAParty()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(lordId, out var lord));

            Assert.False(Descriptor().CreationCaptureStrategy.TryBuildDebugCapture(lord, out _));
        });
    }

    [Fact]
    public void Accept_TheServerAddsTheMercenariesToTheOwnersParty()
    {
        SetOwner();

        Server.Call(() =>
        {
            var issue = AttachQuest(Server, companyTroopCount: 5);
            var strategy = Descriptor().QuestSolutionAcceptStrategy;

            strategy.Apply(issue, strategy.Capture(issue));
        });
        TestEnvironment.FlushCoalescer();

        Assert.Equal(5, MercenariesIn(Server));
        Assert.Equal(5, MercenariesIn(Owner));
    }

    [Fact]
    public void SellingTheCompany_PaysTheOwnerFromTheServersOwnCount()
    {
        GiveMercenaries(7);
        var goldBefore = 0;
        var goldAfter = 0;
        Quest quest = null!;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
            quest = (Quest)AttachQuest(Server, companyTroopCount: 2).IssueQuest;
            goldBefore = ownerHero.Gold;

            RunBranchAsOwner(ownerHero, quest, LandLordCompanyOfTroubleBranch.SuccessBySellingCompany);

            goldAfter = ownerHero.Gold;
        }, CharacterLinks);
        TestEnvironment.FlushCoalescer();

        Assert.Equal(1000 + (7 * 150), goldAfter - goldBefore);
        Assert.Equal(5, quest.RelationshipChangeWithQuestGiver);
        Assert.Equal(1, questEndings.CountFor(Server));
        Assert.Equal(0, MercenariesIn(Server));
        Assert.Equal(0, MercenariesIn(Owner));
    }

    [Fact]
    public void LettingTheCompanyLeave_RemovesThemOnceAndFailsTheQuest()
    {
        GiveMercenaries(6);
        Quest quest = null!;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
            quest = (Quest)AttachQuest(Server, companyTroopCount: 6).IssueQuest;

            RunBranchAsOwner(ownerHero, quest, LandLordCompanyOfTroubleBranch.FailByCompanyLeaving);
        }, CharacterLinks);
        TestEnvironment.FlushCoalescer();

        Assert.Equal(-2, quest.RelationshipChangeWithQuestGiver);
        Assert.Equal(1, questEndings.CountFor(Server));
        Assert.Equal(0, MercenariesIn(Server));
        Assert.Equal(0, MercenariesIn(Owner));
    }

    [Fact]
    public void FinalizingOnTheOwnersClient_LeavesTheMercenariesToTheServer()
    {
        GiveMercenaries(4);

        Owner.Call(() =>
        {
            Assert.True(Owner.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            var quest = (Quest)AttachQuest(Owner, companyTroopCount: 4).IssueQuest;

            using (new AllowedThread())
            {
                Campaign.Current.MainParty = ownerParty;
                quest.OnFinalize();
            }
        });

        Assert.Equal(4, MercenariesIn(Owner));
    }

    [Fact]
    public void DismissingTheQuest_RemovesTheMercenariesFromTheOwnersParty()
    {
        GiveMercenaries(4);
        SetOwner();

        Server.Call(() =>
        {
            var issue = AttachQuest(Server, companyTroopCount: 4);

            issue.IssueFinalized();
        });
        TestEnvironment.FlushCoalescer();

        Assert.Equal(0, MercenariesIn(Server));
        Assert.Equal(0, MercenariesIn(Owner));
    }

    [Fact]
    public void FoodTheft_TheServerRemovesOneOfTheItemFromTheOwnersParty()
    {
        SetOwner();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            ownerParty.ItemRoster.AddToCounts(DefaultItems.Grain, 3);
        });
        TestEnvironment.FlushCoalescer();

        var serverGrainBefore = GrainIn(Server);
        var ownerGrainBefore = GrainIn(Owner);

        Server.Call(() => AttachQuest(Server, companyTroopCount: 4));

        Owner.Call(() =>
        {
            var quest = AttachQuest(Owner, companyTroopCount: 4).IssueQuest;

            Owner.Resolve<IMessageBroker>().Publish(this, new LandLordCompanyOfTroubleFoodStolen(quest, DefaultItems.Grain));
        });
        TestEnvironment.FlushCoalescer();

        Assert.Equal(serverGrainBefore - 1, GrainIn(Server));
        Assert.Equal(ownerGrainBefore - 1, GrainIn(Owner));
    }

    private void RunBranchAsOwner(Hero ownerHero, Quest quest, LandLordCompanyOfTroubleBranch branch)
    {
        using (new MainHeroSubstitutionScope(ownerHero, ownerHero.PartyBelongedTo))
        {
            Assert.True(Descriptor().FinalizationProofStrategy.TryRunBranch(quest, (byte)branch));
        }
    }

    private void SetOwner()
    {
        Server.Call(() => Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(lordId, IssueId, OwnerControllerId)));
    }

    private void GiveMercenaries(int count)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));

            ownerParty.MemberRoster.AddToCounts(troop, count);
        });
        TestEnvironment.FlushCoalescer();

        Assert.Equal(count, MercenariesIn(Owner));
    }

    private int MercenariesIn(EnvironmentInstance instance)
    {
        var count = 0;

        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));

            count = ownerParty.MemberRoster.GetTroopCount(troop);
        });

        return count;
    }

    private int GrainIn(EnvironmentInstance instance)
    {
        var count = 0;

        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));

            count = ownerParty.ItemRoster.GetItemNumber(DefaultItems.Grain);
        });

        return count;
    }

    private IssueBase AttachQuest(EnvironmentInstance instance, int companyTroopCount)
    {
        Assert.True(instance.ObjectManager.TryGetObject<Hero>(lordId, out var lord));
        Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));

        var quest = (Quest)RuntimeHelpers.GetUninitializedObject(typeof(Quest));
        quest.QuestGiver = lord;
        quest._troubleCharacterObject = troop;
        quest._companyTroopCount = companyTroopCount;
        quest._questState = QuestBase.QuestStates.Ongoing;
        AccessTools.Field(typeof(QuestBase), nameof(QuestBase._journalEntries)).SetValue(quest, new MBList<JournalLog>());

        lord.Issue.IssueQuest = quest;
        return lord.Issue;
    }

    private IQuestTypeDescriptor Descriptor()
    {
        Assert.True(Server.Container.Resolve<IQuestTypeRegistry>().TryGetByName(IssueName, out var descriptor));
        return descriptor;
    }

    private void GiveTheLordTheIssue(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(lordId, out var lord));

            using (new AllowedThread())
            {
                var issue = new Issue(lord);
                issue.StringId = IssueId;
                issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;

                Campaign.Current.IssueManager._issues.Add(lord, issue);
                lord.OnIssueCreatedForHero(issue);
            }
        });
    }
}
