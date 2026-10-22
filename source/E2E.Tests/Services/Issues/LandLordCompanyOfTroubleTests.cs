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
using SandBox.GauntletUI.Map;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
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

    private static readonly MethodBase[] MapEventVisuals =
    {
        AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)),
    };

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
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));

            using (new AllowedThread())
            {
                troop.StringId = "company_of_trouble_character";
            }

            MBObjectManager.Instance.RegisterObject(troop);

            var issue = AttachQuest(Server, companyTroopCount: 4);
            ((Quest)issue.IssueQuest)._troubleCharacterObject = null;

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

    [Fact]
    public void BeatingTheCompany_PaysTheOwnerAndSucceeds()
    {
        var companyPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        SetOwner();
        var goldBefore = GoldOfOwner();
        Quest quest = null!;

        Server.Call(() => quest = CompanyTurnedHostile(companyPartyId));
        EndFightOnServer(companyPartyId, BattleState.AttackerVictory);
        TestEnvironment.FlushCoalescer();

        Assert.Equal(500, GoldOfOwner() - goldBefore);
        Assert.Equal(5, quest.RelationshipChangeWithQuestGiver);
        Assert.Equal(1, questEndings.Count);
    }

    [Fact]
    public void LosingToTheCompany_FailsTheQuest()
    {
        var companyPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        SetOwner();
        Quest quest = null!;

        Server.Call(() => quest = CompanyTurnedHostile(companyPartyId));
        EndFightOnServer(companyPartyId, BattleState.DefenderVictory);

        Assert.Equal(-2, quest.RelationshipChangeWithQuestGiver);
        Assert.Equal(1, questEndings.Count);
    }

    [Fact]
    public void AnotherBattle_AfterTheCompanyTurnedHostile_DoesNotEndTheQuest()
    {
        var companyPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var otherPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        SetOwner();
        var goldBefore = GoldOfOwner();

        Server.Call(() => CompanyTurnedHostile(companyPartyId));
        EndFightOnServer(otherPartyId, BattleState.AttackerVictory);
        TestEnvironment.FlushCoalescer();

        Assert.Equal(0, questEndings.Count);
        Assert.Equal(goldBefore, GoldOfOwner());
    }

    [Fact]
    public void EndingTheQuest_DestroysTheHostileCompany()
    {
        var companyPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        MobileParty company = null!;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            var quest = CompanyTurnedHostile(companyPartyId);
            company = quest._companyOfTroubleParty;

            using (new AllowedThread())
            {
                Campaign.Current.MainParty = ownerParty;
            }

            quest.OnFinalize();
        });
        Server.PumpGameThread();

        Assert.False(company.IsActive);
    }

    [Fact]
    public void EndingTheQuest_LeavesACompanyThatIsInABattle()
    {
        var companyPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var otherPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        MobileParty company = null!;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(otherPartyId, out var other));
            var quest = CompanyTurnedHostile(companyPartyId);
            company = quest._companyOfTroubleParty;
            StartFieldBattle(other, company);

            quest.OnFinalize();
        }, MapEventVisuals);
        Server.PumpGameThread();

        Assert.True(company.IsActive);
        Assert.False(company.IsCurrentlyUsedByAQuest);
    }

    [Fact]
    public void ASimulatedBattleTheOwnerIsNotIn_DoesNotEndTheQuest()
    {
        var attackerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var defenderPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        SetOwner();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(attackerPartyId, out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(defenderPartyId, out var defender));
            AttachQuest(Server, companyTroopCount: 0);

            var mapEvent = StartFieldBattle(attacker, defender);
            mapEvent.IsPlayerSimulation = true;
            mapEvent._battleState = BattleState.AttackerVictory;

            CampaignEventDispatcher.Instance.OnMapEventEnded(mapEvent);
        }, MapEventVisuals.Concat(CharacterLinks));

        Assert.Equal(0, questEndings.Count);
    }

    [Fact]
    public void TheCompanyDestroyedBySomeoneElse_FailsTheQuest()
    {
        var companyPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var otherPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        SetOwner();
        Quest quest = null!;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(otherPartyId, out var other));
            quest = CompanyTurnedHostile(companyPartyId);

            CampaignEventDispatcher.Instance.OnMobilePartyDestroyed(quest._companyOfTroubleParty, other.Party);
        });

        Assert.Equal(-2, quest.RelationshipChangeWithQuestGiver);
        Assert.Equal(1, questEndings.Count);
    }

    [Fact]
    public void TheCompanyDestroyedBySomeoneElse_IsNotDestroyedAgainWhenTheQuestEnds()
    {
        var companyPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var otherPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        SetOwner();
        MobileParty company = null!;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(otherPartyId, out var other));
            var quest = CompanyTurnedHostile(companyPartyId);
            company = quest._companyOfTroubleParty;

            using (new AllowedThread())
            {
                Campaign.Current.MainParty = ownerParty;
            }

            CampaignEventDispatcher.Instance.OnMobilePartyDestroyed(company, other.Party);
            quest.OnFinalize();
        });
        Server.PumpGameThread();

        Assert.True(company.IsActive);
    }

    [Fact]
    public void TheOwnerDestroyingTheCompany_LeavesTheEndingToTheBattle()
    {
        var companyPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        SetOwner();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            var quest = CompanyTurnedHostile(companyPartyId);

            CampaignEventDispatcher.Instance.OnMobilePartyDestroyed(quest._companyOfTroubleParty, ownerParty.Party);
        });

        Assert.Equal(0, questEndings.Count);
    }

    [Fact]
    public void DismissingAQuestNobodyOwns_RemovesTheMercenariesAndTheCompany()
    {
        var companyPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        GiveMercenaries(4);
        MobileParty company = null!;

        Server.Call(() =>
        {
            var quest = CompanyTurnedHostile(companyPartyId);
            company = quest._companyOfTroubleParty;

            Assert.True(Server.ObjectManager.TryGetObject<Hero>(lordId, out var lord));
            lord.Issue.IssueFinalized();
        });
        Server.PumpGameThread();
        TestEnvironment.FlushCoalescer();

        Assert.Equal(0, MercenariesIn(Server));
        Assert.Equal(0, MercenariesIn(Owner));
        Assert.False(company.IsActive);
    }

    [Fact]
    public void TheLeaveDialog_OpensForMercenariesInTheOwnersParty()
    {
        Assert.True(CompanyDialogOpens(conversationPartyId: ownerPartyId));
    }

    [Fact]
    public void TheLeaveDialog_DoesNotOpenInAnEncounterWithTheHostileCompany()
    {
        var companyPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Assert.False(CompanyDialogOpens(conversationPartyId: companyPartyId));
    }

    private bool CompanyDialogOpens(string conversationPartyId)
    {
        var opens = false;

        Owner.Call(() =>
        {
            Assert.True(Owner.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            Assert.True(Owner.ObjectManager.TryGetObject<MobileParty>(conversationPartyId, out var conversationParty));
            var quest = (Quest)AttachQuest(Owner, companyTroopCount: 4).IssueQuest;
            var conversation = Campaign.Current.ConversationManager;

            using (new AllowedThread())
            {
                Campaign.Current.MainParty = ownerParty;
                conversation._conversationParty = conversationParty;
                conversation._conversationAgents.Clear();
                conversation._conversationAgents.Add(new ConversationAgentStub(quest._troubleCharacterObject));
            }

            opens = quest.CompanyDialogFromCondition();
        }, CharacterLinks.Append(AccessTools.PropertyGetter(typeof(CharacterObject), nameof(CharacterObject.EncyclopediaLinkWithName))));

        return opens;
    }

    private Quest CompanyTurnedHostile(string companyPartyId)
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(companyPartyId, out var company));

        var quest = (Quest)AttachQuest(Server, companyTroopCount: 0).IssueQuest;
        quest._companyOfTroubleParty = company;
        company.SetPartyUsedByQuest(true);
        return quest;
    }

    private void EndFightOnServer(string enemyPartyId, BattleState battleState)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(enemyPartyId, out var enemy));

            var mapEvent = StartFieldBattle(ownerParty, enemy);
            mapEvent._battleState = battleState;

            CampaignEventDispatcher.Instance.OnMapEventEnded(mapEvent);
        }, CharacterLinks.Concat(MapEventVisuals));
    }

    private static MapEvent StartFieldBattle(MobileParty attacker, MobileParty defender)
    {
        var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
        mapEvent.MapEventVisual = ObjectHelper.SkipConstructor<GauntletMapEventVisual>();
        mapEvent.Initialize(attacker.Party, defender.Party, new FieldBattleEventComponent(mapEvent), MapEvent.BattleTypes.FieldBattle);
        mapEvent.MapEventVisual = null;
        return mapEvent;
    }

    private int GoldOfOwner()
    {
        var gold = 0;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
            gold = ownerHero.Gold;
        });

        return gold;
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
        AccessTools.Field(typeof(QuestBase), nameof(QuestBase.RewardGold)).SetValue(quest, 500);

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
