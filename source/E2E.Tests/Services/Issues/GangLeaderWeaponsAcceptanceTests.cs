using Common.Util;
using E2E.Tests.Environment;
using GameInterface.Services.Issues.Generic.CreationCapture;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

using Issue = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;
using Quest = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

public sealed class GangLeaderWeaponsAcceptanceTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    public GangLeaderWeaponsAcceptanceTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    private string CreateIssueCopies()
    {
        var giverId = environment.CreateRegisteredObject<Hero>();
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        var townId = environment.CreateRegisteredObject<Town>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        foreach (var instance in environment.Clients)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(townId, out var town));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                using (new AllowedThread())
                {
                    settlement.SetSettlementComponent(town);
                    giver.StayingInSettlement = settlement;
                    giver.Occupation = Occupation.GangLeader;
                    Campaign.Current.MainParty = party;
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    var runner = new CreationCaptureRunner<Issue, GangLeaderWeaponsCreationFields>(
                        instance.Resolve<IGangLeaderWeaponsAcceptance>(), IssueBase.IssueFrequency.Common);
                    runner.ConstructAndRegisterReplicated(giver,
                        new GangLeaderWeaponsCreationFields(0, 200, CampaignTime.Days(15)));
                }
            });
        }
        return giverId;
    }

    [Fact]
    public void MirrorPreservesAuthoritativeProgressWithEmptyObserverInventory()
    {
        var giverId = CreateIssueCopies();
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.Empty(MobileParty.MainParty.ItemRoster);
                var fields = new GangLeaderWeaponsQuestFields("weapons_authoritative_quest",
                    CampaignTime.Days(25), 4700, 0, 21, 0.75f, 200, 17);

                client.Resolve<IGangLeaderWeaponsAcceptance>().MirrorQuestAccepted(giver, fields);

                var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
                Assert.Equal(fields.QuestId, quest.StringId);
                Assert.Equal(fields.DueTime, quest.QuestDueTime);
                Assert.Equal(17, quest._collectedItemAmount);
                Assert.Equal(17, quest._playerStartsQuestLog.CurrentProgress);
                Assert.Equal(21, quest._requestedWeaponAmount);
                Assert.Equal(1050, quest._bribeGold);
                Assert.Equal(4700, quest.RewardGold);
                Assert.Equal(0.75f, quest._issueDifficulty);
                Assert.Empty(MobileParty.MainParty.ItemRoster);
            });
        }
    }

    [Fact]
    public void DuplicateAcceptanceDoesNotReplaceQuestOrResetProgress()
    {
        var giverId = CreateIssueCopies();
        var client = environment.Clients.First();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            var acceptance = client.Resolve<IGangLeaderWeaponsAcceptance>();
            var fields = new GangLeaderWeaponsQuestFields("weapons_duplicate_quest",
                CampaignTime.Days(25), 4700, 0, 21, 0.75f, 200, 17);
            acceptance.MirrorQuestAccepted(giver, fields);
            var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
            quest.SetCurrentItemAmount(20);
            var journal = quest._playerStartsQuestLog;

            acceptance.MirrorQuestAccepted(giver, fields);

            Assert.Same(quest, giver.Issue.IssueQuest);
            Assert.Same(journal, quest._playerStartsQuestLog);
            Assert.Equal(20, quest._collectedItemAmount);
            Assert.Equal(20, journal.CurrentProgress);
        });
    }
}
