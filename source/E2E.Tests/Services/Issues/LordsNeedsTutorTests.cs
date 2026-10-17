using Common.Messaging;
using Common.Network.Messages;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.LordsNeedsTutor;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest;

namespace E2E.Tests.Services.Issues;

public sealed class LordsNeedsTutorTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private readonly string giverId;
    private readonly string pupilId;
    private readonly string settlementId;
    private readonly string jewelryId;
    private readonly string[] partyIds;
    private readonly string[] heroIds;
    private readonly QuestScenario scenario;
    private readonly Dictionary<string, int> originalFocus = new();
    private EnvironmentInstance Server => environment.Server;
    private EnvironmentInstance Owner => environment.Clients.First();
    private EnvironmentInstance Observer => environment.Clients.Last();
    private IEnumerable<EnvironmentInstance> Instances => environment.Clients.Prepend(Server);

    public LordsNeedsTutorTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        var skillHandles = new Dictionary<string, uint>();
        Server.Call(() =>
        {
            foreach (var skill in Skills.All)
            {
                Assert.True(Server.ObjectManager.AddExisting("SkillObject_" + skill.StringId, skill));
                Assert.True(Server.ObjectManager.TryGetHandle(skill, out var handle));
                skillHandles.Add(skill.StringId, handle);
            }
        });
        foreach (var client in environment.Clients)
            client.Call(() =>
            {
                foreach (var skill in Skills.All)
                    Assert.True(client.ObjectManager.AddExisting("SkillObject_" + skill.StringId, skill, skillHandles[skill.StringId]));
            });
        giverId = environment.CreateRegisteredObject<Hero>();
        pupilId = environment.CreateRegisteredObject<Hero>();
        settlementId = environment.CreateRegisteredObject<Settlement>();
        jewelryId = environment.CreateRegisteredObject<ItemObject>();
        partyIds = environment.Clients.Select(_ => environment.CreateRegisteredObject<MobileParty>()).ToArray();
        heroIds = new string[2];
        Server.Call(() =>
        {
            for (var i = 0; i < 2; i++)
            {
                var party = Get<MobileParty>(Server, partyIds[i]);
                Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out heroIds[i]));
                Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(Controller(i), heroIds[i], partyIds[i], "", "")));
            }
        });
        for (var i = 0; i < 2; i++)
        {
            var client = environment.Clients.ElementAt(i);
            environment.ConnectRegisteredPlayer(client, Controller(i));
            client.Resolve<IControllerIdProvider>().SetControllerId(Controller(i));
        }
        foreach (var instance in Instances)
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    new JournalLogsCampaignBehavior().RegisterEvents();
                    new IssuesCampaignBehavior().RegisterEvents();
                    var jewelry = Get<ItemObject>(instance, jewelryId);
                    jewelry.StringId = "tutor_test_jewelry";
                    jewelry.Name = new TextObject("Tutor test jewelry");
                    jewelry.ItemType = ItemObject.ItemTypeEnum.Goods;
                    jewelry.ItemCategory = DefaultItemCategories.Jewelry;
                    Game.Current.ObjectManager.RegisterObject(jewelry);
                    var giver = Get<Hero>(instance, giverId);
                    var pupil = Get<Hero>(instance, pupilId);
                    var settlement = Get<Settlement>(instance, settlementId);
                    giver.Clan.SetLeader(giver);
                    pupil.Clan = giver.Clan;
                    pupil.SetBirthDay(CampaignTime.YearsFromNow(-18));
                    pupil.StayingInSettlement = settlement;
                    giver.StayingInSettlement = settlement;
                    for (var i = 0; i < 2; i++)
                    {
                        var party = Get<MobileParty>(instance, partyIds[i]);
                        party.CurrentSettlement = settlement;
                        party.ActualClan.Tier = 2;
                        if (instance != Server)
                            Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(new Player(Controller(i), heroIds[i], partyIds[i], "", "")));
                    }
                    if (instance != Server)
                    {
                        var index = instance == Owner ? 0 : 1;
                        Game.Current.PlayerTroop = Get<Hero>(instance, heroIds[index]).CharacterObject;
                        Campaign.Current.MainParty = Get<MobileParty>(instance, partyIds[index]);
                        Campaign.Current.PlayerDefaultFaction = Get<Hero>(instance, heroIds[index]).Clan;
                    }
                }
            });
        Server.Call(() =>
        {
            foreach (var skill in Skills.All) originalFocus[skill.StringId] = Get<Hero>(Server, pupilId).HeroDeveloper.GetFocus(skill);
        });
        CreateIssue(giverId, pupilId);
        scenario = new QuestScenario(environment, giverId);
    }

    public void Dispose() => environment.Dispose();
    private static string Controller(int i) => "tutor-player-" + i;
    private static T Get<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }

    private void CreateIssue(string giver, string pupil) => Server.Call(() =>
    {
        var young = Get<Hero>(Server, pupil);
        var potential = new PotentialIssueData((in PotentialIssueData _, Hero owner) => new Issue(owner, young), typeof(Issue), IssueBase.IssueFrequency.Common);
        Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, Get<Hero>(Server, giver)));
    });

    private void Accept()
    {
        scenario.Accept(Owner);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
        Owner.Call(() => ((Quest)scenario.QuestOf(Owner)).QuestAcceptedConsequences());
        environment.FlushCoalescer();
    }

    [Fact]
    public void CreationUsesTheServersPupilAndIssueId()
    {
        foreach (var instance in Instances)
            instance.Call(() => Assert.Same(Get<Hero>(instance, pupilId), Assert.IsType<Issue>(Get<Hero>(instance, giverId).Issue)._youngHero));
    }

    [Fact]
    public void AcceptanceStartsOnlyTheOwnersJournalAndAddsThePupilOnce()
    {
        Accept();
        foreach (var instance in Instances)
            instance.Call(() =>
            {
                var pupil = Get<Hero>(instance, pupilId);
                var party = Get<MobileParty>(instance, partyIds[0]);
                Assert.Same(party.ActualClan, pupil.CompanionOf);
                foreach (var skill in Skills.All)
                    Assert.True(originalFocus[skill.StringId] + 1 == pupil.HeroDeveloper.GetFocus(skill),
                        $"{(instance == Server ? "server" : "client")} {skill.StringId} focus was not granted");
                Assert.Equal(1, party.MemberRoster.GetTroopCount(pupil.CharacterObject));
                Assert.Equal(0, Get<MobileParty>(instance, partyIds[1]).MemberRoster.GetTroopCount(pupil.CharacterObject));
                var quest = Get<Hero>(instance, giverId).Issue.IssueQuest;
                if (instance == Observer) Assert.Null(quest);
                else
                {
                    Assert.Single(quest.JournalEntries);
                    Assert.Equal(instance == Owner, Campaign.Current.QuestManager.Quests.Contains(quest));
                }
            });
    }

    [Theory]
    [InlineData(15, false)]
    [InlineData(30, true)]
    [InlineData(45, true)]
    public void AuthoritativeSkillProgressUpdatesOnlyTheOwnersQuest(int gain, bool ready)
    {
        Accept();
        Server.Call(() => Get<Hero>(Server, pupilId).HeroDeveloper.ChangeSkillLevel(DefaultSkills.OneHanded, gain));
        environment.FlushCoalescer();
        Owner.Call(() =>
        {
            var quest = (Quest)scenario.QuestOf(Owner);
            Assert.Equal(Math.Min(gain, 30), quest._startQuestLog.CurrentProgress);
            Assert.Equal(ready, quest._questCompletedStartConversation);
        });
        Observer.Call(() => Assert.Null(Get<Hero>(Observer, giverId).Issue.IssueQuest));
    }

    [Theory]
    [InlineData("success")]
    [InlineData("dismiss")]
    [InlineData("timeout")]
    [InlineData("leader")]
    public void TerminalFlowsEndTheOwnerQuestAndCleanUpThePupil(string ending)
    {
        Accept();
        var expectedGift = ((Quest)scenario.QuestOf(Owner))._randomForQuestReward;
        if (ending == "success")
        {
            Owner.Call(() => ((Quest)scenario.QuestOf(Owner)).FirstConversationEndConsequence());
            Server.Call(() => Get<Hero>(Server, pupilId).HeroDeveloper.ChangeSkillLevel(DefaultSkills.OneHanded, 30));
            environment.FlushCoalescer();
            Owner.Call(() => ((Quest)scenario.QuestOf(Owner)).QuestCompletedWithSuccessAfterConversation());
        }
        else Server.Call(() =>
        {
            var pupil = Get<Hero>(Server, pupilId);
            if (ending == "dismiss") RemoveCompanionAction.ApplyByFire(pupil.CompanionOf, pupil);
            if (ending == "leader") CampaignEventDispatcher.Instance.OnClanLeaderChanged(Get<Hero>(Server, giverId), pupil);
            if (ending == "timeout")
            {
                ((Quest)Get<Hero>(Server, giverId).Issue.IssueQuest).ChangeQuestDueTime(CampaignTime.DaysFromNow(-1));
                CampaignEventDispatcher.Instance.HourlyTick();
            }
        });
        environment.FlushCoalescer();
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        foreach (var instance in Instances)
            instance.Call(() =>
            {
                var pupil = Get<Hero>(instance, pupilId);
                Assert.Null(Get<Hero>(instance, giverId).Issue);
                Assert.Null(pupil.CompanionOf);
                foreach (var skill in Skills.All) Assert.Equal(originalFocus[skill.StringId], pupil.HeroDeveloper.GetFocus(skill));
                Assert.Equal(0, Get<MobileParty>(instance, partyIds[0]).MemberRoster.GetTroopCount(pupil.CharacterObject));
                Assert.Equal(0, Get<MobileParty>(instance, partyIds[1]).ItemRoster.GetItemNumber(Get<ItemObject>(instance, jewelryId)));
                var jewels = Get<MobileParty>(instance, partyIds[0]).ItemRoster.GetItemNumber(Get<ItemObject>(instance, jewelryId));
                Assert.Equal(ending == "success" ? expectedGift : 0, jewels);
                var archive = Campaign.Current.LogEntryHistory.FindLastGameActionLog((JournalLogEntry entry) => entry.RelatedHero == Get<Hero>(instance, giverId));
                if (instance == Observer) Assert.Null(archive);
                else Assert.NotNull(archive);
            });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FrameworkAllowsConcurrentPlayersAndRefusesTheSamePlayersSecondQuest(bool anotherPlayer)
    {
        Accept();
        var secondGiver = environment.CreateRegisteredObject<Hero>();
        var secondPupil = environment.CreateRegisteredObject<Hero>();
        foreach (var instance in Instances)
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var giver = Get<Hero>(instance, secondGiver);
                    var pupil = Get<Hero>(instance, secondPupil);
                    giver.Clan.SetLeader(giver);
                    pupil.Clan = giver.Clan;
                    pupil.SetBirthDay(CampaignTime.YearsFromNow(-18));
                    pupil.StayingInSettlement = Get<Settlement>(instance, settlementId);
                    giver.StayingInSettlement = pupil.StayingInSettlement;
                }
            });
        CreateIssue(secondGiver, secondPupil);
        var second = new QuestScenario(environment, secondGiver);
        var accepter = anotherPlayer ? Observer : Owner;
        second.Accept(accepter);
        if (anotherPlayer)
        {
            Assert.Equal(2, Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>().Count());
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
            accepter.Call(() => ((Quest)second.QuestOf(accepter)).QuestAcceptedConsequences());
            Owner.Call(() => Assert.Null(Get<Hero>(Owner, secondGiver).Issue.IssueQuest));
            Observer.Call(() => Assert.Null(Get<Hero>(Observer, giverId).Issue.IssueQuest));
        }
        else
        {
            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
            Server.Call(() => Assert.True(Get<Hero>(Server, secondGiver).Issue.IsOngoingWithoutQuest));
        }
    }

    [Fact]
    public void VanillaClanTierRefusalDoesNotAttachThePupil()
    {
        Server.Call(() =>
        {
            using (new AllowedThread()) Get<MobileParty>(Server, partyIds[0]).ActualClan.Tier = 1;
        });
        scenario.Accept(Owner);
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Server.Call(() => Assert.Null(Get<Hero>(Server, pupilId).CompanionOf));
    }

    [Fact]
    public void ServerMirrorKeepsThePupilRestrictionsWithoutStartingQuestListeners()
    {
        Accept();
        Server.Call(() =>
        {
            var pupil = Get<Hero>(Server, pupilId);
            var result = true;
            Campaign.Current.QuestManager.CanHeroLeadParty(pupil, ref result);
            Assert.False(result);
            result = true;
            Campaign.Current.QuestManager.CanHeroMarry(pupil, ref result);
            Assert.False(result);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DismissalCleansUpThePupilWhenOwnerOrServerLeaves(bool serverRestart)
    {
        Accept();
        Server.Call(() =>
        {
            if (serverRestart)
            {
                Server.Resolve<IIssueOwnershipRegistry>().Remove(giverId);
                Server.Resolve<IMessageBroker>().Publish(this, new GameInterface.Registry.Messages.AllGameObjectsRegistered());
            }
            else
            {
                Server.Resolve<IPlayerManager>().ClearPeer(Owner.NetPeer);
                Server.Resolve<IMessageBroker>().Publish(this, new PlayerDisconnected(Owner.NetPeer, default));
            }
            var pupil = Get<Hero>(Server, pupilId);
            Assert.Null(Get<Hero>(Server, giverId).Issue);
            Assert.Null(pupil.CompanionOf);
            foreach (var skill in Skills.All) Assert.Equal(originalFocus[skill.StringId], pupil.HeroDeveloper.GetFocus(skill));
            Assert.Equal(0, Get<MobileParty>(Server, partyIds[0]).MemberRoster.GetTroopCount(pupil.CharacterObject));
        });
    }

    [Fact]
    public void AnotherPlayersDisconnectKeepsTheOwnerQuest()
    {
        Accept();
        Server.Call(() =>
        {
            Server.Resolve<IPlayerManager>().ClearPeer(Observer.NetPeer);
            Server.Resolve<IMessageBroker>().Publish(this, new PlayerDisconnected(Observer.NetPeer, default));
            Assert.True(Get<Hero>(Server, giverId).Issue.IssueQuest.IsOngoing);
        });
    }

    [Fact]
    public void PupilDeathFailsTheQuestWithoutAReward()
    {
        Accept();
        Server.Call(() => CampaignEventDispatcher.Instance.OnHeroKilled(Get<Hero>(Server, pupilId), null,
            KillCharacterAction.KillCharacterActionDetail.Murdered, false));
        Assert.Equal(IssueOutcome.QuestFail, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Owner.Call(() => Assert.Empty(Campaign.Current.QuestManager.Quests));
    }

    [Fact]
    public void ReceivedSkillEventsCannotChangeProgressOnTheClient()
    {
        Accept();
        Owner.Call(() =>
        {
            var quest = (Quest)scenario.QuestOf(Owner);
            using (new AllowedThread()) quest.OnHeroGainedSkill(quest._youngHero, DefaultSkills.OneHanded, 30);
            Assert.Equal(0, quest._startQuestLog.CurrentProgress);
            Assert.False(quest._questCompletedStartConversation);
        });
    }

    [Fact]
    public void EarlySuccessRequestDoesNotRewardOrEndTheQuest()
    {
        Accept();
        Owner.Call(() => ((Quest)scenario.QuestOf(Owner)).QuestCompletedWithSuccessAfterConversation());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Owner.Call(() => Assert.True(scenario.QuestOf(Owner).IsOngoing));
    }
}
