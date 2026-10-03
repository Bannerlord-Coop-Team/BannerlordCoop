using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.Migrated.CapturedByBountyHunters;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

using Issue = CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue;
using Quest = CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest;

public class CapturedByBountyHuntersIssueTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private bool journalsRegistered;

    public CapturedByBountyHuntersIssueTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    private (string Giver, string Hideout) CreateIssue()
    {
        var giverId = environment.CreateRegisteredObject<Hero>();
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        var townId = environment.CreateRegisteredObject<Town>();
        var hideoutSettlementId = environment.CreateRegisteredObject<Settlement>();
        var hideoutId = environment.CreateRegisteredObject<Hideout>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(townId, out var town));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideoutSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<Hideout>(hideoutId, out var hideout));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                using (new AllowedThread())
                {
                    settlement.SetSettlementComponent(town);
                    giver.StayingInSettlement = settlement;
                    giver.Occupation = Occupation.GangLeader;
                    giver.Clan = null;
                    hideoutSettlement.SetSettlementComponent(hideout);
                    Campaign.Current.MainParty = party;
                    party.CurrentSettlement = settlement;
                    Game.Current.PlayerTroop = giver.CharacterObject;
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    if (Campaign.Current.GameMenuManager.GetGameMenu("hideout_place") == null)
                    {
                        var menu = new GameMenu("hideout_place");
                        menu.Initialize(new TextObject("Hideout"), null, GameMenu.MenuOverlayType.None);
                        Campaign.Current.GameMenuManager.AddGameMenu(menu);
                    }
                    var minimumParties = Math.Max(1, Campaign.Current.Models.BanditDensityModel.NumberOfMinimumBanditPartiesInAHideoutToInfestIt);
                    for (var i = 0; i < minimumParties; i++)
                    {
                        var clan = GameObjectCreator.CreateInitializedObject<Clan>();
                        clan.Culture = GameObjectCreator.CreateInitializedObject<CultureObject>();
                        var bandits = BanditPartyComponent.CreateBanditParty("bounty-bandits-" + i, clan,
                            hideout, false, null, new CampaignVec2(Vec2.Zero, true));
                        bandits.CurrentSettlement = hideoutSettlement;
                    }
                    Campaign.Current.PlayerTraitDeveloper ??= new PropertyOwner<PropertyObject>();
                }
                if (!journalsRegistered)
                {
                    new JournalLogsCampaignBehavior().RegisterEvents();
                    new IssuesCampaignBehavior().RegisterEvents();
                }
            });
        }
        journalsRegistered = true;
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(environment.Server.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideout));
            PotentialIssueData.StartIssueDelegate create = (in PotentialIssueData _, Hero hero) => new Issue(hero, hideout);
            var potential = new PotentialIssueData(create, typeof(Issue), IssueBase.IssueFrequency.Common);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, giver));
        });
        return (giverId, hideoutSettlementId);
    }

    [Fact]
    public void CreationReplicatesTheSelectedHideoutAndGenerationToBothClients()
    {
        var fixture = CreateIssue();
        var sent = Assert.Single(environment.Server.NetworkSentMessages.GetMessages<NetworkCapturedByBountyHuntersIssueCreated>());
        Assert.Equal(fixture.Hideout, sent.HideoutId);
        Assert.False(string.IsNullOrEmpty(sent.IssueId));
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                var issue = Assert.IsType<Issue>(giver.Issue);
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(fixture.Hideout, out var hideout));
                Assert.Same(hideout, issue._hideout);
                Assert.Equal(sent.IssueId, issue.StringId);
                Assert.Equal(sent.DueTime, issue.IssueDueTime);
                Assert.Equal(sent.Difficulty, issue.IssueDifficultyMultiplier);
                Assert.True(client.Resolve<IIssueGenerationRegistry>().TryGetGeneration(giver, out var generation));
                Assert.Equal(sent.Generation, generation);
                client.Resolve<IMessageBroker>().Publish(null, sent);
                Assert.Same(issue, giver.Issue);
            });
        }
    }

    [Fact]
    public void AiLordCompletionRemovesTheUnacceptedIssueFromEveryInstance()
    {
        var fixture = CreateIssue();
        var solverId = environment.CreateRegisteredObject<Hero>();
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(solverId, out var solver));
            using (new AllowedThread()) solver.Occupation = Occupation.Lord;
            Assert.True(giver.Issue.IsOngoingWithoutQuest);
            giver.Issue.CompleteIssueWithAiLord(solver);
            Assert.Null(giver.Issue);
            Assert.True(Campaign.Current.IssueManager.HasIssueCoolDown(typeof(Issue), giver));
        });
        Assert.Single(environment.Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                Assert.Null(giver.Issue);
                Assert.Empty(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>());
            });
        }
    }

    [Fact]
    public void RejectedServerFinalizationDoesNotRemoveClientIssues()
    {
        var fixture = CreateIssue();
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            var issue = giver.Issue;
            issue.IssueFinalized();
            Assert.Same(issue, giver.Issue);
        });
        Assert.Empty(environment.Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                Assert.IsType<Issue>(giver.Issue);
            });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnerJournalUpdatesDoNotRepeatOratoryRewards(bool deferredReceive)
    {
        var fixture = CreateIssue();
        Assert.True(TaleWorlds.Library.MathF.Round(DefaultPerks.Charm.Oratory.PrimaryBonus) > 0);
        var playerId = AcceptFromFirstClient(fixture);
        var clanId = environment.CreateRegisteredObject<Clan>();
        var kingdomId = environment.CreateRegisteredObject<Kingdom>();
        foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(playerId, out var player));
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
                Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
                using (new AllowedThread())
                {
                    player.Clan = clan;
                    clan.SetLeader(player);
                    clan.Kingdom = kingdom;
                    clan.Renown = 100f;
                    clan.Influence = 100f;
                    player.HeroDeveloper.AddPerk(DefaultPerks.Charm.Oratory);
                }
                Assert.True(player.GetPerkValue(DefaultPerks.Charm.Oratory));
            });
        }
        var router = environment.Server.Resolve<TestNetworkRouter>();
        if (deferredReceive) router.ReceiveContext = TestNetworkReceiveContext.PollerThread;
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Assert.True(environment.Server.Resolve<IBountyHuntersQuestContext>().TryOpen(giver, out var scope));
            using (scope) ((Quest)giver.Issue.IssueQuest).SuccessConsequences();
        });
        if (deferredReceive)
        {
            foreach (var client in environment.Clients) client.PumpGameThread();
        }
        Assert.Single(environment.Server.NetworkSentMessages.GetMessages<NetworkBountyHuntersJournal>()
            .Where(message => message.Status == IssueBase.IssueUpdateDetails.IssueFinishedWithSuccess));
        foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
                var expected = 100f + TaleWorlds.Library.MathF.Round(DefaultPerks.Charm.Oratory.PrimaryBonus);
                Assert.Equal(expected, clan.Renown);
                Assert.Equal(expected, clan.Influence);
            });
        }
    }

    [Fact]
    public void AcceptedQuestCreatesAJournalOnlyForTheRecordedPlayer()
    {
        var fixture = CreateIssue();
        var owner = environment.Clients.First();
        owner.Resolve<IControllerIdProvider>().SetControllerId("bounty-owner");
        environment.Clients.Last().Resolve<IControllerIdProvider>().SetControllerId("other-player");
        CampaignTime due = default;
        environment.Server.Call(() =>
        {
            due = CampaignTime.DaysFromNow(30);
            var journal = new[] { new JournalLog(CampaignTime.Now, new TextObject("Accepted at the server settlement")) };
            var fields = GenericAcceptFieldsSerializer.Serialize(new BountyHuntersQuestAcceptFields(due, 0.25f, journal));
            environment.Server.Resolve<INetwork>().SendAll(new NetworkQuestTypeQuestAccepted(fixture.Giver, "bounty-owner", fields));
        });
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                Assert.True(giver.Issue.IsSolvingWithQuest);
                if (client == owner)
                {
                    var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
                    Assert.True(quest.IsOngoing);
                    Assert.Equal(due, quest.QuestDueTime);
                    Assert.Single(quest.JournalEntries);
                    Assert.Contains(quest, Campaign.Current.QuestManager.Quests);
                }
                else
                {
                    Assert.Null(giver.Issue.IssueQuest);
                    Assert.DoesNotContain(Campaign.Current.QuestManager.Quests, quest => quest is Quest);
                }
            });
        }
    }
    [Fact]
    public void AlternativeAcceptanceKeepsTheReturnJournalOnTheRecordedClient()
    {
        var fixture = CreateIssue();
        var companionId = environment.CreateRegisteredObject<Hero>();
        var owner = environment.Clients.First();
        owner.Resolve<IControllerIdProvider>().SetControllerId("bounty-owner");
        environment.Clients.Last().Resolve<IControllerIdProvider>().SetControllerId("other-player");
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            using (new AllowedThread())
            {
                giver.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
            }
            var state = new AlternativeSolutionVanillaState(CampaignTime.DaysFromNow(5), CampaignTime.DaysFromNow(4), 0.2f, 2, 1000, null);
            var journal = new[] { new JournalLog(CampaignTime.Now, new TextObject("The companion departed"), new TextObject("Return Days"), 0, 5) };
            var fields = GenericAcceptFieldsSerializer.Serialize(new BountyHuntersAlternativeAcceptFields(state, 0.25f, journal));
            var troops = environment.Server.Resolve<ITroopRosterInterface>().PackTroopRosterData(giver.Issue.AlternativeSolutionSentTroops);
            environment.Server.Resolve<INetwork>().SendAll(new NetworkQuestTypeAlternativeAccepted(fixture.Giver, "bounty-owner", state, fields, troops));
        });
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                Assert.True(giver.Issue.IsSolvingWithAlternative);
                Assert.Equal(2, giver.Issue._alternativeSolutionCasualtyCount);
                if (client == owner) Assert.Single(giver.Issue.JournalEntries);
                else Assert.Empty(giver.Issue.JournalEntries);
            });
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("previous-owner")]
    public void MalformedAcceptancePreservesThePreviousOwner(string previousOwner)
    {
        var fixture = CreateIssue();
        var client = environment.Clients.First();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            var owners = client.Resolve<IIssueOwnershipRegistry>();
            if (previousOwner != null) owners.SetOwner(giver, previousOwner);
            client.Resolve<IMessageBroker>().Publish(null, new NetworkQuestTypeQuestAccepted(fixture.Giver, "new-owner", new byte[] { 0x0f }));
            Assert.Equal(previousOwner != null, owners.TryGetOwnerControllerId(giver, out var restored));
            Assert.Equal(previousOwner, restored);
            Assert.True(giver.Issue.IsOngoingWithoutQuest);
        });
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("previous-owner", false)]
    [InlineData("previous-owner", true)]
    public void MalformedAlternativeAcceptanceClearsTheReceivedClaim(string previousOwner, bool invalidPayload)
    {
        var fixture = CreateIssue();
        var client = environment.Clients.First();
        AcceptFromFirstClient(fixture, alternative: true, submitAlternative: false, beforeRequest: () =>
        {
            var giver = client.GetRegisteredObject<Hero>(fixture.Giver);
            var sent = giver.Issue.AlternativeSolutionSentTroops;
            var companion = sent.GetTroopRoster().Single(entry => entry.Character.IsHero).Character;
            var troop = sent.GetTroopRoster().Single(entry => !entry.Character.IsHero).Character;
            var troops = client.Resolve<ITroopRosterInterface>().PackTroopRosterData(sent);
            var state = new AlternativeSolutionVanillaState(CampaignTime.DaysFromNow(5), CampaignTime.DaysFromNow(4), 0.2f, 2, 1000, null);
            var fields = invalidPayload ? new byte[] { 0x0f } : GenericAcceptFieldsSerializer.Serialize(
                new BountyHuntersAlternativeAcceptFields(state, 0.25f, Array.Empty<JournalLog>()));
            var owners = client.Resolve<IIssueOwnershipRegistry>();
            owners.Clear(giver);
            if (previousOwner != null) owners.SetOwner(giver, previousOwner);

            client.Resolve<IMessageBroker>().Publish(null,
                new NetworkQuestTypeAlternativeAccepted(fixture.Giver, "new-owner", state, fields, troops));

            Assert.Equal(previousOwner != null, owners.TryGetOwnerControllerId(giver, out var restored));
            Assert.Equal(previousOwner, restored);
            Assert.True(giver.Issue.IsOngoingWithoutQuest);
            Assert.Equal(0, sent.TotalManCount);
            Assert.Equal(1, MobileParty.MainParty.MemberRoster.GetTroopCount(companion));
            Assert.Equal(25, MobileParty.MainParty.MemberRoster.GetTroopCount(troop));
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void LosingAlternativeSelectionKeepsItsTroops(bool winningAlternative, bool replyDuringSelection)
    {
        var fixture = CreateIssue();
        var loser = environment.Clients.Last();
        var router = environment.Server.Resolve<TestNetworkRouter>();
        string companionId = null;
        string troopId = null;
        string partyId = null;
        AcceptFromFirstClient(fixture, alternative: true, clientIndex: 1, beforeRequest: () =>
        {
            Assert.True(loser.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            var selected = giver.Issue.AlternativeSolutionSentTroops;
            var companion = selected.GetTroopRoster().Single(entry => entry.Character.IsHero).Character;
            var troop = selected.GetTroopRoster().Single(entry => !entry.Character.IsHero).Character;
            Assert.True(loser.ObjectManager.TryGetId(companion.HeroObject, out companionId));
            Assert.True(loser.ObjectManager.TryGetId(troop, out troopId));
            Assert.True(loser.ObjectManager.TryGetId(MobileParty.MainParty, out partyId));
            Assert.Equal(0, MobileParty.MainParty.MemberRoster.GetTroopCount(companion));
            Assert.Equal(15, MobileParty.MainParty.MemberRoster.GetTroopCount(troop));
            if (replyDuringSelection) AcceptFromFirstClient(fixture, alternative: winningAlternative);
            else router.SetLatency(loser.NetPeer, environment.Server.NetPeer, TimeSpan.FromSeconds(1));
        });
        if (!replyDuringSelection) AcceptFromFirstClient(fixture, alternative: winningAlternative);
        router.AdvanceBy(TimeSpan.FromSeconds(1));
        foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
                Assert.Equal(25, party.MemberRoster.GetTroopCount(troop));
                Assert.Same(party, companion.PartyBelongedTo);
                Assert.False(companion.IsDisabled);
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(giver, out var owner));
                Assert.Equal("bounty-owner", owner);
                Assert.Equal(winningAlternative, giver.Issue.IsSolvingWithAlternative);
                Assert.Equal(winningAlternative ? 11 : 0, giver.Issue.AlternativeSolutionSentTroops.TotalManCount);
                Assert.Equal(0, giver.Issue.AlternativeSolutionSentTroops.GetTroopCount(companion.CharacterObject));
                foreach (var sent in giver.Issue.AlternativeSolutionSentTroops.GetTroopRoster())
                    Assert.Equal(0, party.MemberRoster.GetTroopCount(sent.Character));
            });
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void OpenSelectionCannotEditAnotherPlayersAcceptedRoster(bool winningAlternative, bool resetSelection)
    {
        var fixture = CreateIssue();
        var loser = environment.Clients.Last();
        string partyId = null;
        string companionId = null;
        string troopId = null;
        AcceptFromFirstClient(fixture, alternative: true, clientIndex: 1, beforeRequest: () =>
        {
            Assert.True(loser.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            var party = MobileParty.MainParty;
            var sent = giver.Issue.AlternativeSolutionSentTroops;
            var companion = sent.GetTroopRoster().Single(entry => entry.Character.IsHero).Character;
            var troop = sent.GetTroopRoster().Single(entry => !entry.Character.IsHero).Character;
            Assert.True(loser.ObjectManager.TryGetId(party, out partyId));
            Assert.True(loser.ObjectManager.TryGetId(companion.HeroObject, out companionId));
            Assert.True(loser.ObjectManager.TryGetId(troop, out troopId));
            using (new AllowedThread())
            {
                troop.UpgradeTargets = Array.Empty<CharacterObject>();
                sent.AddToCounts(troop, -10);
                party.MemberRoster.AddToCounts(troop, 10);
            }
            var logic = OpenAlternativeSelection(party, sent);
            var state = Game.Current.GameStateManager.ActiveState;
            var command = new PartyScreenLogic.PartyCommand();
            command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member, troop, 10, 0, -1);
            using (new AllowedThread()) logic.TransferTroop(command, false);
            Assert.Equal(15, party.MemberRoster.GetTroopCount(troop));
            logic.SavePartyScreenData();
            AcceptFromFirstClient(fixture, alternative: winningAlternative, troopIdOverride: troopId);
            Assert.NotSame(state, Game.Current.GameStateManager.ActiveState);
            {
                using (new AllowedThread())
                {
                    if (resetSelection) logic.Reset(true);
                    else
                    {
                        command.FillForTransferTroop(winningAlternative ? PartyScreenLogic.PartyRosterSide.Left : PartyScreenLogic.PartyRosterSide.Right,
                            PartyScreenLogic.TroopType.Member, troop, 1, 0, -1);
                        logic.TransferTroop(command, false);
                    }
                }
            }
        });
        foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
                Assert.Equal(25, party.MemberRoster.GetTroopCount(troop));
                Assert.Equal(winningAlternative ? 11 : 0, giver.Issue.AlternativeSolutionSentTroops.TotalManCount);
                Assert.False(giver.Issue.AlternativeSolutionSentTroops.Contains(companion.CharacterObject));
            });
        }
    }

    [Theory]
    [InlineData(false, false, false, false, false, false, false, false)]
    [InlineData(true, false, false, false, false, false, false, false)]
    [InlineData(false, true, false, false, false, false, false, false)]
    [InlineData(true, true, false, false, false, false, false, false)]
    [InlineData(false, true, true, false, false, false, false, false)]
    [InlineData(true, true, true, false, false, false, false, false)]
    [InlineData(false, false, false, true, false, false, false, false)]
    [InlineData(true, false, false, true, false, false, false, false)]
    [InlineData(false, true, true, true, false, false, false, false)]
    [InlineData(true, true, true, true, false, false, false, false)]
    [InlineData(false, true, false, false, true, false, false, false)]
    [InlineData(true, true, false, false, true, false, false, false)]
    [InlineData(false, false, false, true, false, true, false, false)]
    [InlineData(true, false, false, true, false, true, false, false)]
    [InlineData(false, false, false, true, false, true, true, false)]
    [InlineData(true, false, false, true, false, true, true, false)]
    [InlineData(false, true, false, true, false, true, true, false)]
    [InlineData(true, true, false, true, false, true, true, false)]
    [InlineData(false, true, false, true, false, false, false, true)]
    [InlineData(true, true, false, true, false, false, false, true)]
    [InlineData(false, true, true, true, false, false, false, true)]
    [InlineData(true, true, true, true, false, false, false, true)]
    [InlineData(false, true, false, true, false, true, false, false)]
    [InlineData(true, true, false, true, false, true, false, false)]
    public void PartyScreenDoneKeepsTroopsStagedUntilAlternativeAcceptance(
        bool decline, bool upgrade, bool deferredReceive, bool repeatedDone, bool splitTransfer, bool editAfterDone,
        bool resetAfterEdit, bool reselectAfterDone)
        => CheckPartyScreenSelection(decline, upgrade, deferredReceive, repeatedDone, splitTransfer, editAfterDone,
            resetAfterEdit, reselectAfterDone, false);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void PartyScreenDoneKeepsClaimXpOutOfLaterUpgrades(bool decline, bool deferredReceive)
        => CheckPartyScreenSelection(decline, true, deferredReceive, true, false, false, false, true, true);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void PartyScreenDoneReturningTheWholeSelectionDoesNotDuplicateTroops(bool decline, bool upgrade)
        => CheckPartyScreenSelection(decline, upgrade, false, true, false, false, false, true, false, true);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void PartyScreenDoneConsumesRequiredItems(bool decline, bool deferredReceive)
        => CheckPartyScreenSelection(decline, true, deferredReceive, false, false, false, false, false, false,
            requiresItem: true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PartyScreenDoneKeepsThePartyLockedAfterReopening(bool deferredReceive)
    {
        var fixture = CreateIssue();
        var client = environment.Clients.First();
        AcceptFromFirstClient(fixture, alternative: true, submitAlternative: false, beforeRequest: () =>
        {
            var giver = client.GetRegisteredObject<Hero>(fixture.Giver);
            var party = MobileParty.MainParty;
            var sent = giver.Issue.AlternativeSolutionSentTroops;
            var troop = sent.GetTroopRoster().Single(entry => !entry.Character.IsHero).Character;
            using (new AllowedThread()) troop.UpgradeTargets = Array.Empty<CharacterObject>();
            if (deferredReceive) environment.Server.Resolve<TestNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.PollerThread;
            OpenAlternativeSelection(party, sent);
            Helpers.PartyScreenHelper.CloseScreen(false);
            Assert.Null(Game.Current.GameStateManager.ActiveState);
            var replacement = OpenAlternativeSelection(party, TroopRoster.CreateDummyTroopRoster());
            replacement._partyScreenMode = Helpers.PartyScreenHelper.PartyScreenMode.Normal;
            ((PartyState)Game.Current.GameStateManager.ActiveState).PartyScreenMode = Helpers.PartyScreenHelper.PartyScreenMode.Normal;
            var selection = client.Resolve<IAlternativeSolutionTroopSelection>();
            Assert.True(selection.IsCommitPending(replacement));
            var command = new PartyScreenLogic.PartyCommand();
            command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member, troop, 1, 0, -1);
            Assert.False(replacement.ValidateCommand(command));
            environment.Server.PumpGameThread();
            foreach (var recipient in environment.Clients) recipient.PumpGameThread();
            Assert.False(selection.IsCommitPending(replacement));
            Assert.True(replacement.ValidateCommand(command));
            Helpers.PartyScreenHelper.CloseScreen(false, true);
            selection.Rollback(giver, closeScreen: false);
        });
        environment.Server.PumpGameThread();
        foreach (var recipient in environment.Clients) recipient.PumpGameThread();
    }

    [Theory]
    [InlineData("add", false)]
    [InlineData("add", true)]
    [InlineData("set", false)]
    [InlineData("set", true)]
    [InlineData("wound", false)]
    [InlineData("wound", true)]
    [InlineData("xp", false)]
    [InlineData("xp", true)]
    [InlineData("invalidate", false)]
    [InlineData("invalidate", true)]
    public void PartyScreenDoneRebasesSelectionAgainstWorldChanges(string change, bool deferredReceive)
    {
        var fixture = CreateIssue();
        var client = environment.Clients.First();
        var targetId = environment.CreateRegisteredObject<CharacterObject>();
        string partyId = null;
        string troopId = null;
        int expectedNumber = change == "add" ? 30 : change == "set" ? 20 : change == "invalidate" ? 5 : 25;
        int expectedWounded = change == "wound" ? 4 : 0;
        int expectedXp = 0;
        AcceptFromFirstClient(fixture, alternative: true, submitAlternative: false, beforeRequest: () =>
        {
            var giver = client.GetRegisteredObject<Hero>(fixture.Giver);
            var party = MobileParty.MainParty;
            var sent = giver.Issue.AlternativeSolutionSentTroops;
            var troop = sent.GetTroopRoster().Single(entry => !entry.Character.IsHero).Character;
            Assert.True(client.ObjectManager.TryGetId(party, out partyId));
            Assert.True(client.ObjectManager.TryGetId(troop, out troopId));
            foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
            {
                instance.Call(() =>
                {
                    using (new AllowedThread())
                    {
                        var original = instance.GetRegisteredObject<CharacterObject>(troopId);
                        var target = instance.GetRegisteredObject<CharacterObject>(targetId);
                        target.Level = 26;
                        target.UpgradeTargets = Array.Empty<CharacterObject>();
                        original.UpgradeTargets = change == "xp" ? new[] { target } : Array.Empty<CharacterObject>();
                    }
                });
            }
            var logic = OpenAlternativeSelection(party, sent);
            if (deferredReceive) environment.Server.Resolve<TestNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.PollerThread;
            Assert.True(logic.DoneLogic(true));
            environment.Server.PumpGameThread();
            foreach (var recipient in environment.Clients) recipient.PumpGameThread();
            Assert.False(client.Resolve<IAlternativeSolutionTroopSelection>().IsCommitPending(logic));
            Assert.NotSame(party.MemberRoster, logic.MemberRosters[1]);
            logic.SavePartyScreenData();
            var command = new PartyScreenLogic.PartyCommand();
            command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Left, PartyScreenLogic.TroopType.Member, troop, 1, 0, -1);
            using (new AllowedThread()) logic.TransferTroop(command, false);
            Assert.Equal(16, logic.MemberRosters[1].GetTroopCount(troop));
            environment.Server.Call(() =>
            {
                var authority = environment.Server.GetRegisteredObject<MobileParty>(partyId);
                var original = environment.Server.GetRegisteredObject<CharacterObject>(troopId);
                int index = authority.MemberRoster.FindIndexOfTroop(original);
                switch (change)
                {
                    case "add": authority.MemberRoster.AddToCounts(original, 5); break;
                    case "set": authority.MemberRoster.SetElementNumber(index, 20); break;
                    case "wound": authority.MemberRoster.SetElementWoundedNumber(index, 4); break;
                    case "xp":
                        expectedXp = original.GetUpgradeXpCost(authority.Party, 0);
                        authority.MemberRoster.AddXpToTroop(original, expectedXp);
                        break;
                    case "invalidate": authority.MemberRoster.AddToCounts(original, -20); break;
                }
            });
            environment.FlushCoalescer();
            foreach (var recipient in environment.Clients) recipient.PumpGameThread();
            Assert.Equal(expectedNumber, party.MemberRoster.GetTroopCount(troop));
            if (change == "invalidate")
            {
                Assert.Null(Game.Current.GameStateManager.ActiveState);
                Assert.Equal(0, sent.TotalManCount);
            }
            else
            {
                Assert.Equal(9, sent.GetTroopCount(troop));
                Assert.Equal(expectedNumber - 9, logic.MemberRosters[1].GetTroopCount(troop));
                Assert.Equal(expectedWounded, logic.MemberRosters[1].TotalWoundedRegulars);
                Assert.Equal(expectedXp, logic.MemberRosters[1].GetElementXp(troop));
                logic.ResetToLastSavedPartyScreenData(true);
                Assert.Equal(10, sent.GetTroopCount(troop));
                Assert.Equal(expectedNumber - 10, logic.MemberRosters[1].GetTroopCount(troop));
                using (new AllowedThread()) logic.TransferTroop(command, false);
                logic.Reset(true);
                Assert.Equal(10, sent.GetTroopCount(troop));
                Assert.Equal(expectedNumber - 10, logic.MemberRosters[1].GetTroopCount(troop));
                client.Resolve<IAlternativeSolutionTroopSelection>().Rollback(giver);
            }
        });
        environment.Server.PumpGameThread();
        foreach (var recipient in environment.Clients) recipient.PumpGameThread();
        foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                var troop = instance.GetRegisteredObject<CharacterObject>(troopId);
                Assert.Equal(expectedNumber, party.MemberRoster.GetTroopCount(troop));
                Assert.Equal(expectedWounded, party.MemberRoster.TotalWoundedRegulars);
                Assert.Equal(instance == environment.Server || instance == client ? expectedXp : 0,
                    party.MemberRoster.GetElementXp(troop));
            });
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void PartyScreenDoneRejectionClosesSelectionWithoutSpendingGold(bool deferredReceive, bool closeBeforeReply)
    {
        var fixture = CreateIssue();
        var client = environment.Clients.First();
        var targetId = environment.CreateRegisteredObject<CharacterObject>();
        AcceptFromFirstClient(fixture, alternative: true, submitAlternative: false, beforeRequest: () =>
        {
            var giver = client.GetRegisteredObject<Hero>(fixture.Giver);
            var party = MobileParty.MainParty;
            var sent = giver.Issue.AlternativeSolutionSentTroops;
            var troop = sent.GetTroopRoster().Single(entry => !entry.Character.IsHero).Character;
            Assert.True(client.ObjectManager.TryGetId(troop, out var troopId));
            Assert.True(client.ObjectManager.TryGetId(party, out var partyId));
            Assert.True(client.ObjectManager.TryGetId(Hero.MainHero, out var playerId));
            foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
            {
                instance.Call(() =>
                {
                    using (new AllowedThread())
                    {
                        var original = instance.GetRegisteredObject<CharacterObject>(troopId);
                        var target = instance.GetRegisteredObject<CharacterObject>(targetId);
                        original.Level = 20;
                        target.Level = 26;
                        target.UpgradeTargets = Array.Empty<CharacterObject>();
                        original.UpgradeTargets = new[] { target };
                    }
                });
            }
            environment.Server.Call(() =>
            {
                var authority = environment.Server.GetRegisteredObject<MobileParty>(partyId);
                var original = environment.Server.GetRegisteredObject<CharacterObject>(troopId);
                var player = environment.Server.GetRegisteredObject<Hero>(playerId);
                player.ChangeHeroGold(10000 - player.Gold);
                authority.MemberRoster.AddXpToTroop(original, 2 * original.GetUpgradeXpCost(authority.Party, 0));
            });
            environment.FlushCoalescer();
            var logic = OpenAlternativeSelection(party, sent);
            var command = new PartyScreenLogic.PartyCommand();
            command.FillForUpgradeTroop(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member, troop, 1, 0, -1);
            using (new AllowedThread())
            {
                Assert.True(logic.ValidateCommand(command));
                logic.UpgradeTroop(command);
            }
            if (deferredReceive) environment.Server.Resolve<TestNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.PollerThread;
            if (closeBeforeReply)
            {
                Helpers.PartyScreenHelper.CloseScreen(false);
                Assert.Null(Game.Current.GameStateManager.ActiveState);
            }
            else Assert.True(logic.DoneLogic(true));
            Assert.True(client.Resolve<IAlternativeSolutionTroopSelection>().IsCommitPending(logic));
            environment.Server.Call(() =>
            {
                var authority = environment.Server.GetRegisteredObject<MobileParty>(partyId);
                var original = environment.Server.GetRegisteredObject<CharacterObject>(troopId);
                authority.MemberRoster.SetElementXp(authority.MemberRoster.FindIndexOfTroop(original), 0);
            });
            environment.Server.PumpGameThread();
            foreach (var recipient in environment.Clients) recipient.PumpGameThread();
            Assert.False(client.Resolve<IAlternativeSolutionTroopSelection>().IsCommitPending(logic));
            Assert.Null(Game.Current.GameStateManager.ActiveState);
            Assert.Equal(0, sent.TotalManCount);
            foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
            {
                instance.Call(() =>
                {
                    var authority = instance.GetRegisteredObject<MobileParty>(partyId);
                    Assert.Equal(25, authority.MemberRoster.GetTroopCount(instance.GetRegisteredObject<CharacterObject>(troopId)));
                    Assert.Equal(0, authority.MemberRoster.GetTroopCount(instance.GetRegisteredObject<CharacterObject>(targetId)));
                    Assert.Equal(10000, instance.GetRegisteredObject<Hero>(playerId).Gold);
                });
            }
        });
        environment.Server.PumpGameThread();
        foreach (var recipient in environment.Clients) recipient.PumpGameThread();
        Assert.Empty(environment.Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAlternativeAccepted>());
    }

    [Theory]
    [InlineData(1, false, 10, "none")]
    [InlineData(1, true, 10, "none")]
    [InlineData(23, false, 10, "none")]
    [InlineData(23, true, 10, "none")]
    [InlineData(23, false, 25, "none")]
    [InlineData(23, true, 25, "none")]
    [InlineData(23, false, 10, "saved")]
    [InlineData(23, true, 10, "saved")]
    [InlineData(23, false, 10, "reset")]
    [InlineData(23, true, 10, "reset")]
    public void PartyScreenDonePreservesXpEarnedAfterACommit(
        int initialUpgradeXp, bool deferredReceive, int selectedCount, string restoreSelection)
        => CheckPartyScreenXpChange(initialUpgradeXp, deferredReceive, selectedCount, restoreSelection, true, false);

    [Theory]
    [InlineData(false, false, false, 10)]
    [InlineData(false, false, true, 10)]
    [InlineData(false, false, false, 25)]
    [InlineData(false, false, true, 25)]
    [InlineData(false, true, false, 10)]
    [InlineData(false, true, true, 10)]
    [InlineData(true, true, false, 10)]
    [InlineData(true, true, true, 10)]
    public void PartyScreenDonePreservesXpAtInitialSelectionAndCheaperUpgrades(
        bool commitBeforeChange, bool cheapUpgrade, bool deferredReceive, int selectedCount)
        => CheckPartyScreenXpChange(cheapUpgrade ? 24 : 23, deferredReceive, selectedCount, "none", commitBeforeChange, cheapUpgrade);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PartyScreenDonePreservesXpWhenReturningTroopsAfterACheaperUpgrade(
        bool commitBeforeChange, bool deferredReceive)
        => CheckPartyScreenXpChange(24, deferredReceive, 10, "none", commitBeforeChange, true, returnAfterUpgrade: true);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PartyScreenDonePreservesXpWhenCheaperUpgradeRemovesRemainingStack(
        bool commitBeforeChange, bool deferredReceive)
        => CheckPartyScreenXpChange(24, deferredReceive, 24, "none", commitBeforeChange, true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InitialSelectionRollbackPreservesReceivedCountAndXp(bool deferredReceive)
        => CheckPartyScreenXpChange(25, deferredReceive, 10, "none", false, false, loseTroop: true);

    private void CheckPartyScreenXpChange(int initialUpgradeXp, bool deferredReceive, int selectedCount,
        string restoreSelection, bool commitBeforeChange, bool cheapUpgrade, bool returnAfterUpgrade = false,
        bool loseTroop = false)
    {
        var fixture = CreateIssue();
        var client = environment.Clients.First();
        var targetId = environment.CreateRegisteredObject<CharacterObject>();
        var cheaperTargetId = cheapUpgrade ? environment.CreateRegisteredObject<CharacterObject>() : null;
        AcceptFromFirstClient(fixture, alternative: true, submitAlternative: false, beforeRequest: () =>
        {
            var giver = client.GetRegisteredObject<Hero>(fixture.Giver);
            var party = MobileParty.MainParty;
            var sent = giver.Issue.AlternativeSolutionSentTroops;
            var troop = sent.GetTroopRoster().Single(entry => !entry.Character.IsHero).Character;
            Assert.True(client.ObjectManager.TryGetId(party, out var partyId));
            Assert.True(client.ObjectManager.TryGetId(troop, out var troopId));
            Assert.True(client.ObjectManager.TryGetId(Hero.MainHero, out var playerId));
            foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
            {
                instance.Call(() =>
                {
                    using (new AllowedThread())
                    {
                        var original = instance.GetRegisteredObject<CharacterObject>(troopId);
                        var target = instance.GetRegisteredObject<CharacterObject>(targetId);
                        original.Level = 20;
                        target.Level = 26;
                        target.UpgradeTargets = Array.Empty<CharacterObject>();
                        original.UpgradeTargets = new[] { target };
                        if (cheapUpgrade)
                        {
                            var cheaperTarget = instance.GetRegisteredObject<CharacterObject>(cheaperTargetId);
                            cheaperTarget.Level = 21;
                            cheaperTarget.UpgradeTargets = Array.Empty<CharacterObject>();
                            original.UpgradeTargets = new[] { target, cheaperTarget };
                        }
                    }
                });
            }
            using (new AllowedThread())
            {
                sent.AddToCounts(troop, -10);
                party.MemberRoster.AddToCounts(troop, 10);
            }
            int cost = troop.GetUpgradeXpCost(party.Party, 0);
            environment.Server.Call(() =>
            {
                var player = environment.Server.GetRegisteredObject<Hero>(playerId);
                player.ChangeHeroGold(10000 - player.Gold);
            });
            environment.Server.Call(() => environment.Server.GetRegisteredObject<MobileParty>(partyId).MemberRoster
                .AddXpToTroop(environment.Server.GetRegisteredObject<CharacterObject>(troopId), initialUpgradeXp * cost));
            environment.FlushCoalescer();
            var logic = OpenAlternativeSelection(party, sent);
            var command = new PartyScreenLogic.PartyCommand();
            command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member, troop, selectedCount, 0, -1);
            using (new AllowedThread()) logic.TransferTroop(command, false);
            if (deferredReceive) environment.Server.Resolve<TestNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.PollerThread;
            if (commitBeforeChange)
            {
                Assert.True(logic.DoneLogic(true));
                FlushCommit();
            }
            if (loseTroop)
            {
                environment.Server.Call(() => environment.Server.GetRegisteredObject<MobileParty>(partyId).MemberRoster
                    .AddToCounts(environment.Server.GetRegisteredObject<CharacterObject>(troopId), -1));
                environment.FlushCoalescer();
                foreach (var recipient in environment.Clients) recipient.PumpGameThread();
                Assert.Equal(24, party.MemberRoster.GetTroopCount(troop));
                Assert.Null(Game.Current.GameStateManager.ActiveState);
                Assert.Equal(0, sent.TotalManCount);
                foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
                {
                    instance.Call(() =>
                    {
                        var roster = instance.GetRegisteredObject<MobileParty>(partyId).MemberRoster;
                        var original = instance.GetRegisteredObject<CharacterObject>(troopId);
                        Assert.Equal(24, roster.GetTroopCount(original));
                        Assert.Equal(instance == environment.Server || instance == client ? initialUpgradeXp * cost : 0,
                            roster.GetElementXp(original));
                    });
                }
                return;
            }
            if (restoreSelection != "none")
            {
                command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Left, PartyScreenLogic.TroopType.Member, troop, 5, 0, -1);
                using (new AllowedThread()) logic.TransferTroop(command, false);
                logic.SavePartyScreenData();
                using (new AllowedThread()) logic.TransferTroop(command, false);
            }
            int spentXp = 0;
            int spentGold = 0;
            if (cheapUpgrade)
            {
                spentXp = troop.GetUpgradeXpCost(party.Party, 1);
                spentGold = troop.GetUpgradeGoldCost(party.Party, 1);
                Assert.InRange(spentXp, 1, cost - 1);
                command.FillForUpgradeTroop(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member, troop, 1, 1, -1);
                using (new AllowedThread())
                {
                    Assert.True(logic.ValidateCommand(command));
                    logic.UpgradeTroop(command);
                }
                if (returnAfterUpgrade)
                {
                    command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Left, PartyScreenLogic.TroopType.Member, troop, 5, 0, -1);
                    using (new AllowedThread()) logic.TransferTroop(command, false);
                    selectedCount -= 5;
                }
            }
            else
            {
                environment.Server.Call(() => environment.Server.GetRegisteredObject<MobileParty>(partyId).MemberRoster
                    .AddXpToTroop(environment.Server.GetRegisteredObject<CharacterObject>(troopId), cost));
            }
            environment.FlushCoalescer();
            foreach (var recipient in environment.Clients) recipient.PumpGameThread();
            int expected = cheapUpgrade ? (initialUpgradeXp * cost) - spentXp : (initialUpgradeXp + 1) * cost;
            if (commitBeforeChange && !cheapUpgrade) Assert.Equal(expected, party.MemberRoster.GetElementXp(troop));
            if (!cheapUpgrade)
            {
                AssertXp(logic.CurrentData);
                AssertXp(logic._initialData);
            }
            if (restoreSelection != "none")
            {
                AssertXp(logic._savedData);
                using (new AllowedThread())
                {
                    if (restoreSelection == "saved") logic.ResetToLastSavedPartyScreenData(false);
                    else logic.Reset(true);
                }
                if (restoreSelection == "saved") selectedCount -= 5;
            }
            Assert.True(logic.DoneLogic(true));
            FlushCommit();
            Assert.Equal(expected, party.MemberRoster.GetElementXp(troop));
            Assert.Equal(expected, logic.MemberRosters[1].GetElementXp(troop) + sent.GetElementXp(troop));
            AssertXp(logic.CurrentData);
            int remainingCount = cheapUpgrade ? 24 : 25;
            Assert.Equal(remainingCount - selectedCount, logic.MemberRosters[1].GetTroopCount(troop));
            Assert.Equal(selectedCount, sent.GetTroopCount(troop));
            foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
            {
                instance.Call(() =>
                {
                    var authority = instance.GetRegisteredObject<MobileParty>(partyId).MemberRoster;
                    var original = instance.GetRegisteredObject<CharacterObject>(troopId);
                    Assert.Equal(remainingCount, authority.GetTroopCount(original));
                    Assert.Equal(instance == environment.Server || instance == client ? expected : 0,
                        authority.GetElementXp(original));
                    if (cheapUpgrade)
                    {
                        Assert.Equal(1, authority.GetTroopCount(instance.GetRegisteredObject<CharacterObject>(cheaperTargetId)));
                        if (instance == environment.Server || instance == client)
                            Assert.Equal(10000 - spentGold, instance.GetRegisteredObject<Hero>(playerId).Gold);
                    }
                });
            }
            client.Resolve<IAlternativeSolutionTroopSelection>().Rollback(giver);

            void AssertXp(PartyScreenData data)
            {
                Assert.Equal(expected, data.RightMemberRoster.GetElementXp(troop) + data.LeftMemberRoster.GetElementXp(troop));
                Assert.InRange(data.RightMemberRoster.GetElementXp(troop), 0, data.RightMemberRoster.GetTroopCount(troop) * cost);
                Assert.InRange(data.LeftMemberRoster.GetElementXp(troop), 0, data.LeftMemberRoster.GetTroopCount(troop) * cost);
            }

            void FlushCommit()
            {
                environment.Server.PumpGameThread();
                foreach (var recipient in environment.Clients) recipient.PumpGameThread();
                Assert.False(client.Resolve<IAlternativeSolutionTroopSelection>().IsCommitPending(logic));
            }
        });
        environment.Server.PumpGameThread();
        foreach (var recipient in environment.Clients) recipient.PumpGameThread();
    }

    [Fact]
    public void PartyScreenDonePackingFailureReleasesTheUnsentCommit()
    {
        var fixture = CreateIssue();
        var client = environment.Clients.First();
        AcceptFromFirstClient(fixture, alternative: true, submitAlternative: false, beforeRequest: () =>
        {
            var giver = client.GetRegisteredObject<Hero>(fixture.Giver);
            var hero = Hero.MainHero;
            var logic = OpenAlternativeSelection(MobileParty.MainParty, giver.Issue.AlternativeSolutionSentTroops);
            Assert.True(client.ObjectManager.TryGetId(hero, out var id));
            Assert.True(client.ObjectManager.TryGetHandle(hero, out var handle));
            Assert.True(client.ObjectManager.Remove(hero));
            try
            {
                Assert.False(logic.DoneLogic(true));
                Assert.False(client.Resolve<IAlternativeSolutionTroopSelection>().IsCommitPending(logic));
                Assert.Null(Game.Current.GameStateManager.ActiveState);
                Assert.Empty(client.NetworkSentMessages.GetMessages<GameInterface.Services.Party.Messages.NetworkCompleteDoneLogic>());
            }
            finally
            {
                Assert.True(client.ObjectManager.AddExisting(id, hero, handle));
            }
        });
        environment.Server.PumpGameThread();
        foreach (var recipient in environment.Clients) recipient.PumpGameThread();
    }

    [Fact]
    public void PartyScreenDoneIgnoresAReplyForAnOlderSelection()
    {
        var client = environment.Clients.First();
        client.Call(() =>
        {
            var selection = client.Resolve<IAlternativeSolutionTroopSelection>();
            var oldLogic = new PartyScreenLogic();
            var currentLogic = new PartyScreenLogic();
            var oldId = selection.BeginCommit(oldLogic);
            var currentId = selection.BeginCommit(currentLogic);
            selection.CompleteCommit(oldId, false);
            Assert.True(selection.IsCommitPending(currentLogic));
            selection.CompleteCommit(currentId, true);
            Assert.False(selection.IsCommitPending(currentLogic));
        });
    }

    private void CheckPartyScreenSelection(
        bool decline, bool upgrade, bool deferredReceive, bool repeatedDone, bool splitTransfer, bool editAfterDone,
        bool resetAfterEdit, bool reselectAfterDone, bool upgradeAfterDone, bool returnAllAfterDone = false,
        bool requiresItem = false)
    {
        var fixture = CreateIssue();
        var client = environment.Clients.First();
        var upgradedId = environment.CreateRegisteredObject<CharacterObject>();
        var requiredItemId = requiresItem ? environment.CreateRegisteredObject<ItemObject>() : null;
        var requiredCategoryId = requiresItem ? environment.CreateRegisteredObject<ItemCategory>() : null;
        string partyId = null;
        string troopId = null;
        string companionId = null;
        int upgradeCost = 0;
        int expectedPartyXp = 0;
        int expectedSentXp = 0;
        string playerId = null;
        bool keepsAdditionalTroops = editAfterDone && !resetAfterEdit;
        AcceptFromFirstClient(fixture, alternative: true, beforeRequest: () =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            var party = MobileParty.MainParty;
            var sent = giver.Issue.AlternativeSolutionSentTroops;
            var troop = sent.GetTroopRoster().Single(entry => !entry.Character.IsHero).Character;
            var companion = sent.GetTroopRoster().Single(entry => entry.Character.IsHero).Character;
            Assert.True(client.ObjectManager.TryGetId(party, out partyId));
            Assert.True(client.ObjectManager.TryGetId(troop, out troopId));
            Assert.True(client.ObjectManager.TryGetId(companion.HeroObject, out companionId));
            using (new AllowedThread())
            {
                troop.UpgradeTargets = Array.Empty<CharacterObject>();
                sent.AddToCounts(troop, -10);
                party.MemberRoster.AddToCounts(troop, 10);
            }
            Assert.True(client.ObjectManager.TryGetId(Hero.MainHero, out playerId));
            if (upgrade)
            {
                foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
                {
                    instance.Call(() =>
                    {
                        var original = instance.GetRegisteredObject<CharacterObject>(troopId);
                        var target = instance.GetRegisteredObject<CharacterObject>(upgradedId);
                        using (new AllowedThread())
                        {
                            original.Level = 20;
                            target.Level = 26;
                            target.UpgradeTargets = Array.Empty<CharacterObject>();
                            original.UpgradeTargets = new[] { target };
                            if (requiresItem)
                            {
                                var category = instance.GetRegisteredObject<ItemCategory>(requiredCategoryId);
                                var item = instance.GetRegisteredObject<ItemObject>(requiredItemId);
                                item.StringId = "bounty_upgrade_item";
                                TaleWorlds.ObjectSystem.MBObjectManager.Instance.RegisterObject(item);
                                item.ItemCategory = category;
                                item.Value = 100;
                                target.UpgradeRequiresItemFromCategory = category;
                                if (Campaign.Current.GetCampaignBehavior<IViewDataTracker>() == null)
                                    Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(
                                        new CampaignBehaviorBase[] { new ViewDataTrackerCampaignBehavior() }));
                            }
                        }
                    });
                }
                environment.Server.Call(() =>
                {
                    var original = environment.Server.GetRegisteredObject<CharacterObject>(troopId);
                    var authoritativeParty = environment.Server.GetRegisteredObject<MobileParty>(partyId);
                    var player = environment.Server.GetRegisteredObject<Hero>(playerId);
                    player.ChangeHeroGold(10000 - player.Gold);
                    authoritativeParty.MemberRoster.AddXpToTroop(original, (splitTransfer ? 24 : 23) * original.GetUpgradeXpCost(authoritativeParty.Party, 0));
                    authoritativeParty.MemberRoster.AddToCounts(original, 0, false, 4);
                    if (requiresItem)
                        authoritativeParty.ItemRoster.AddToCounts(environment.Server.GetRegisteredObject<ItemObject>(requiredItemId), 6);
                });
                environment.FlushCoalescer();
                upgradeCost = 5 * troop.GetUpgradeGoldCost(party.Party, 0);
            }
            int closed = 0;
            var logic = OpenAlternativeSelection(party, sent, (_, _, _, _, _, _, _) =>
            {
                closed++;
                Assert.Equal(keepsAdditionalTroops ? 12 : 10, sent.TotalRegulars);
                if (decline) client.Resolve<IAlternativeSolutionTroopSelection>().Rollback(giver, closeScreen: false);
                else giver.Issue.StartIssueWithAlternativeSolution();
            });
            var command = new PartyScreenLogic.PartyCommand();
            int remainingXp = party.MemberRoster.GetElementXp(troop);
            using (new AllowedThread())
            {
                if (upgrade)
                {
                    remainingXp -= 5 * troop.GetUpgradeXpCost(party.Party, 0);
                    command.FillForUpgradeTroop(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member, troop, 5, 0, -1);
                    Assert.True(logic.ValidateCommand(command));
                    logic.UpgradeTroop(command);
                    if (requiresItem) Assert.Equal(1, party.ItemRoster.GetItemNumber(client.GetRegisteredObject<ItemObject>(requiredItemId)));
                    var target = client.GetRegisteredObject<CharacterObject>(upgradedId);
                    command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member, target, 5, 0, -1);
                    logic.TransferTroop(command, false);
                }
                if (splitTransfer)
                {
                    command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member, troop, 2, 0, -1);
                    logic.TransferTroop(command, false);
                }
                command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member, troop, upgrade ? (splitTransfer ? 3 : 5) : 10, 0, -1);
                logic.TransferTroop(command, false);
            }
            if (deferredReceive) environment.Server.Resolve<TestNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.PollerThread;
            if (upgrade) Assert.True(sent.GetElementXp(troop) > 0);
            if (repeatedDone)
            {
                Assert.True(logic.DoneLogic(true));
                AwaitPartyCommit();
                Assert.True(logic.DoneLogic(true));
                AwaitPartyCommit();
            }
            if (editAfterDone)
            {
                using (new AllowedThread())
                {
                    command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member, troop, 2, 0, -1);
                    logic.TransferTroop(command, false);
                }
            }
            if (resetAfterEdit) logic.Reset(true);
            if (reselectAfterDone)
            {
                using (new AllowedThread())
                {
                    int count = returnAllAfterDone ? logic.MemberRosters[0].GetTroopCount(troop) : 1;
                    command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Left, PartyScreenLogic.TroopType.Member, troop, count, 0, -1);
                    logic.TransferTroop(command, false);
                    if (returnAllAfterDone) Assert.Equal(upgrade ? 20 : 25, logic.MemberRosters[1].GetTroopCount(troop));
                    command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member, troop, count, 0, -1);
                    logic.TransferTroop(command, false);
                }
            }
            if (upgradeAfterDone)
            {
                int cost = troop.GetUpgradeXpCost(party.Party, 0);
                command.FillForUpgradeTroop(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member,
                    troop, (remainingXp / cost) + 1, 0, -1);
                Assert.False(logic.ValidateCommand(command));
                using (new AllowedThread())
                {
                    command.FillForUpgradeTroop(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member, troop, 1, 0, -1);
                    Assert.True(logic.ValidateCommand(command));
                    logic.UpgradeTroop(command);
                }
                remainingXp -= cost;
                upgradeCost += troop.GetUpgradeGoldCost(party.Party, 0);
            }
            int retainedCount = (keepsAdditionalTroops ? 13 : 15) - (upgradeAfterDone ? 1 : 0);
            expectedSentXp = decline || !upgrade ? 0 : Math.Max(0, remainingXp - (retainedCount * troop.GetUpgradeXpCost(party.Party, 0)));
            expectedPartyXp = remainingXp - expectedSentXp;
            Helpers.PartyScreenHelper.CloseScreen(false);
            Assert.Equal(1, closed);
            Assert.Null(Game.Current.GameStateManager.ActiveState);

            void AwaitPartyCommit()
            {
                Assert.True(client.Resolve<IAlternativeSolutionTroopSelection>().IsCommitPending(logic));
                Assert.False(logic.IsDoneActive());
                Assert.False(logic.DoneLogic(true));
                if (upgradeAfterDone)
                {
                    command.FillForUpgradeTroop(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member, troop, 1, 0, -1);
                    Assert.False(logic.ValidateCommand(command));
                }
                environment.Server.PumpGameThread();
                foreach (var recipient in environment.Clients) recipient.PumpGameThread();
                Assert.False(client.Resolve<IAlternativeSolutionTroopSelection>().IsCommitPending(logic));
            }
        }, submitAlternative: false);
        environment.Server.PumpGameThread();
        environment.FlushCoalescer();
        foreach (var recipient in environment.Clients) recipient.PumpGameThread();
        environment.Server.PumpGameThread();
        foreach (var recipient in environment.Clients) recipient.PumpGameThread();
        if (decline) Assert.Empty(environment.Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAlternativeAccepted>());
        else Assert.Single(environment.Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAlternativeAccepted>());
        foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                var troop = instance.GetRegisteredObject<CharacterObject>(troopId);
                var target = instance.GetRegisteredObject<CharacterObject>(upgradedId);
                var companion = instance.GetRegisteredObject<Hero>(companionId);
                var giver = instance.GetRegisteredObject<Hero>(fixture.Giver);
                Assert.Equal((decline ? (upgrade ? 20 : 25) : (keepsAdditionalTroops ? 13 : 15)) - (upgradeAfterDone ? 1 : 0), party.MemberRoster.GetTroopCount(troop));
                Assert.Equal((decline && upgrade ? 5 : 0) + (upgradeAfterDone ? 1 : 0), party.MemberRoster.GetTroopCount(target));
                Assert.Equal(decline ? 1 : 0, party.MemberRoster.GetTroopCount(companion.CharacterObject));
                Assert.Equal(decline ? 0 : (keepsAdditionalTroops ? 13 : 11), giver.Issue.AlternativeSolutionSentTroops.TotalManCount);
                Assert.Equal(instance == environment.Server || instance == client ? expectedPartyXp : 0,
                    party.MemberRoster.GetElementXp(troop));
                Assert.Equal(expectedSentXp, giver.Issue.AlternativeSolutionSentTroops.GetElementXp(troop));
                Assert.Equal(upgrade ? 4 : 0, party.MemberRoster.TotalWoundedRegulars);
                Assert.Equal(0, giver.Issue.AlternativeSolutionSentTroops.TotalWoundedRegulars);
                if (upgrade) Assert.Equal(10000 - upgradeCost, instance.GetRegisteredObject<Hero>(playerId).Gold);
                if (requiresItem) Assert.Equal(1, party.ItemRoster.GetItemNumber(instance.GetRegisteredObject<ItemObject>(requiredItemId)));
            });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IssueRemovalCancelsAnOpenAlternativeSelection(bool afterDone)
    {
        var fixture = CreateIssue();
        var client = environment.Clients.First();
        string partyId = null;
        string troopId = null;
        string companionId = null;
        AcceptFromFirstClient(fixture, alternative: true, submitAlternative: false, beforeRequest: () =>
        {
            var giver = client.GetRegisteredObject<Hero>(fixture.Giver);
            var party = MobileParty.MainParty;
            var sent = giver.Issue.AlternativeSolutionSentTroops;
            var troop = sent.GetTroopRoster().Single(entry => !entry.Character.IsHero).Character;
            var companion = sent.GetTroopRoster().Single(entry => entry.Character.IsHero).Character;
            Assert.True(client.ObjectManager.TryGetId(party, out partyId));
            Assert.True(client.ObjectManager.TryGetId(troop, out troopId));
            Assert.True(client.ObjectManager.TryGetId(companion.HeroObject, out companionId));
            using (new AllowedThread())
            {
                troop.UpgradeTargets = Array.Empty<CharacterObject>();
                sent.AddToCounts(troop, -10);
                party.MemberRoster.AddToCounts(troop, 10);
            }
            var logic = OpenAlternativeSelection(party, sent);
            var command = new PartyScreenLogic.PartyCommand();
            command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member, troop, 10, 0, -1);
            using (new AllowedThread()) logic.TransferTroop(command, false);
            if (afterDone) Assert.True(logic.DoneLogic(false));
            environment.Server.Call(() => environment.Server.GetRegisteredObject<Hero>(fixture.Giver).Issue.CompleteIssueWithCancel());
            Assert.Null(Game.Current.GameStateManager.ActiveState);
            using (new AllowedThread()) logic.Reset(true);
        });
        environment.FlushCoalescer();
        foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                Assert.Null(instance.GetRegisteredObject<Hero>(fixture.Giver).Issue);
                Assert.Equal(25, party.MemberRoster.GetTroopCount(instance.GetRegisteredObject<CharacterObject>(troopId)));
                Assert.Equal(1, party.MemberRoster.GetTroopCount(instance.GetRegisteredObject<Hero>(companionId).CharacterObject));
            });
        }
    }

    private static PartyScreenLogic OpenAlternativeSelection(MobileParty party, TroopRoster sent, PartyScreenClosedDelegate onClosed = null)
    {
        var logic = new PartyScreenLogic();
        var leftPrisoners = TroopRoster.CreateDummyTroopRoster();
        logic.Initialize(new PartyScreenLogicInitializationData
        {
            LeftMemberRoster = sent,
            LeftPrisonerRoster = leftPrisoners,
            RightMemberRoster = party.MemberRoster,
            RightPrisonerRoster = party.PrisonRoster,
            RightOwnerParty = party.Party,
            RightLeaderHero = Hero.MainHero,
            MemberTransferState = PartyScreenLogic.TransferState.Transferable,
            PrisonerTransferState = PartyScreenLogic.TransferState.NotTransferable,
            AccompanyingTransferState = PartyScreenLogic.TransferState.Transferable,
            PartyScreenMode = Helpers.PartyScreenHelper.PartyScreenMode.QuestTroopManage,
            PartyPresentationDoneButtonDelegate = Helpers.PartyScreenHelper.ManageTroopsAndPrisonersDoneHandler,
            PartyScreenClosedDelegate = onClosed,
            TransferHealthiesGetWoundedsFirst = true,
        });
        var states = Game.Current.GameStateManager;
        // Closing the managed screen must not destroy the headless test game.
        states.Owner = Moq.Mock.Of<IGameStateManagerOwner>();
        var state = states.CreateState<PartyState>();
        state.PartyScreenLogic = logic;
        state.PartyScreenMode = Helpers.PartyScreenHelper.PartyScreenMode.QuestTroopManage;
        states._gameStates.Add(state);
        return logic;
    }

    private string AcceptFromFirstClient((string Giver, string Hideout) fixture, bool alternative = false, int clientIndex = 0, Action beforeRequest = null, string troopIdOverride = null, bool submitAlternative = true)
    {
        var playerId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var troopId = troopIdOverride ?? environment.CreateRegisteredObject<CharacterObject>();
        var companionId = alternative ? environment.CreateRegisteredObject<Hero>() : null;
        var client = environment.Clients.ElementAt(clientIndex);
        var controller = clientIndex == 0 ? "bounty-owner" : "other-player";
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(playerId, out var hero));
            Assert.True(environment.Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(environment.Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            using (new AllowedThread())
            {
                hero.Clan.SetLeader(hero);
                troop.Level = 20;
                party.MemberRoster.AddToCounts(hero.CharacterObject, 1);
                party.MemberRoster.AddToCounts(troop, 25);
                party.CurrentSettlement = giver.CurrentSettlement;
                if (alternative)
                {
                    Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                    companion.Clan = hero.Clan;
                    companion.CompanionOf = hero.Clan;
                    companion.PartyBelongedTo = party;
                    party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                }
            }
            Assert.True(environment.Server.Resolve<IPlayerManager>().AddPlayer(new Player(controller, playerId, partyId, "", "")));
        });
        environment.ConnectRegisteredPlayer(client, controller);
        environment.Clients.First().Resolve<IControllerIdProvider>().SetControllerId("bounty-owner");
        environment.Clients.Last().Resolve<IControllerIdProvider>().SetControllerId("other-player");
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(playerId, out var hero));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Game.Current.PlayerTroop = hero.CharacterObject;
            Campaign.Current.MainParty = party;
            ResolvedMainHeroContext.ResolvedMainHero = hero;
            MessageBroker.Instance.Publish(giver, new IssueConversationOpenedLocally(giver, controller));
            if (alternative)
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
                Assert.Equal(25, party.MemberRoster.GetTroopCount(troop));
                using (new AllowedThread())
                {
                    party.MemberRoster.AddToCounts(companion.CharacterObject, -1);
                    party.MemberRoster.AddToCounts(troop, -10);
                    giver.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                    giver.Issue.AlternativeSolutionSentTroops.AddToCounts(troop, 10);
                }
                beforeRequest?.Invoke();
                if (submitAlternative) giver.Issue.StartIssueWithAlternativeSolution();
            }
            else Assert.True(Campaign.Current.IssueManager.StartIssueQuest(giver));
        });
        if (beforeRequest != null) return playerId;
        if (alternative) Assert.Single(environment.Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAlternativeAccepted>().Where(message => message.OwnerId == fixture.Giver));
        else Assert.Single(environment.Server.NetworkSentMessages.GetMessages<NetworkQuestTypeQuestAccepted>().Where(message => message.OwnerId == fixture.Giver));
        return playerId;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AlternativeCompletionRetainsTheOwnersResultAndSurvivors(bool fails)
    {
        var fixture = CreateIssue();
        var playerId = AcceptFromFirstClient(fixture, alternative: true);
        var owner = environment.Clients.First();
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            var issue = Assert.IsType<Issue>(giver.Issue);
            Assert.Equal(11, issue.AlternativeSolutionSentTroops.TotalManCount);
            Assert.True(issue.AlternativeSolutionHero.IsDisabled);
            issue.JournalEntries[0].UpdateCurrentProgress(3);
        });
        owner.Call(() =>
        {
            Assert.True(owner.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Assert.Equal(3, Assert.Single(giver.Issue.JournalEntries).CurrentProgress);
        });
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(playerId, out var player));
            var issue = Assert.IsType<Issue>(giver.Issue);
            var gold = player.Gold;
            var casualties = issue._alternativeSolutionCasualtyCount;
            issue._failureChance = fails ? 1f : -1f;
            issue.AlternativeSolutionReturnTimeForTroops = CampaignTime.Now - CampaignTime.Days(1);
            issue.CompleteIssueWithAlternativeSolution();
            Assert.Null(giver.Issue);
            Assert.Equal(gold + (fails ? 0 : 3000), player.Gold);
            Assert.True(environment.Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet("bounty-owner", out var returned));
            Assert.Equal(11 - casualties, returned.TotalManCount);
            Assert.Equal(1, returned.TotalHeroes);
            if (fails)
            {
                Assert.True(environment.Server.Resolve<PendingRegistry<PropertyOwner<PropertyObject>>>().TryGet(player, out var traits));
                Assert.Equal(-10, traits.GetPropertyValue(DefaultTraits.Honor));
            }
        });
        owner.Call(() =>
        {
            var journal = Assert.Single(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>());
            Assert.True(journal.IsEnded());
            Assert.Equal(fails, journal.IsEndedUnsuccessfully());
            Assert.True(owner.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet("bounty-owner", out var returned));
            Assert.Equal(1, returned.TotalHeroes);
        });
        environment.Clients.Last().Call(() => Assert.Empty(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>()));
    }

    private sealed class TestDataStore : IDataStore
    {
        private readonly Dictionary<string, object> records;
        public bool IsSaving { get; }
        public bool IsLoading => !IsSaving;

        public TestDataStore(bool saving, Dictionary<string, object> records)
        {
            IsSaving = saving;
            this.records = records;
        }

        public bool SyncData<T>(string key, ref T data)
        {
            if (IsSaving) records[key] = data;
            else if (records.TryGetValue(key, out var saved)) data = (T)saved;
            else return false;
            return true;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReloadFiltersAnotherPlayersActiveAndFinishedJournals(bool completed)
    {
        var fixture = CreateIssue();
        AcceptFromFirstClient(fixture);
        var owner = environment.Clients.First();
        Quest quest = null;
        owner.Call(() =>
        {
            Assert.True(owner.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            quest = (Quest)giver.Issue.IssueQuest;
        });
        if (completed)
        {
            environment.Server.Call(() =>
            {
                Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                giver.Issue.IssueQuest.CompleteQuestWithCancel();
            });
        }
        owner.Call(() =>
        {
            Assert.True(owner.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            var journals = owner.Resolve<IBountyHuntersJournalOwners>();
            var saved = new Dictionary<string, object>();
            journals.SyncData(new TestDataStore(true, saved));
            journals.SyncData(new TestDataStore(false, new Dictionary<string, object>()));
            journals.SyncData(new TestDataStore(false, saved));
            journals.FilterLoadedCampaign();
            Assert.Equal(!completed, Campaign.Current.QuestManager.Quests.Contains(quest));
            Assert.Single(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>());

            owner.Resolve<IControllerIdProvider>().SetControllerId("other-player");
            journals.FilterLoadedCampaign();
            Assert.Null(giver.Issue?.IssueQuest);
            Assert.DoesNotContain(quest, Campaign.Current.QuestManager.Quests);
            Assert.Empty(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>());
            Assert.DoesNotContain(Campaign.Current.QuestManager.TrackedObjects, entry => entry.Value.Contains(quest));
        });
    }

    [Fact]
    public void OldJournalUpdatesAndAnotherPlayersCompletionDoNotReplacePersonalProgress()
    {
        var fixture = CreateIssue();
        var otherFixture = CreateIssue();
        AcceptFromFirstClient(fixture);
        AcceptFromFirstClient(otherFixture, clientIndex: 1);
        var owner = environment.Clients.First();
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(otherFixture.Giver, out var other));
            Assert.True(environment.Server.Resolve<IIssueGenerationRegistry>().TryGetGeneration(giver, out var generation));
            var stale = new MBList<JournalLog> { new JournalLog(CampaignTime.Now, new TextObject("Stale result")) };
            environment.Server.Resolve<INetwork>().SendAll(new NetworkBountyHuntersJournal(fixture.Giver, generation - 1,
                true, stale, IssueBase.IssueUpdateDetails.None));
            other.Issue.IssueQuest.CompleteQuestWithTimeOut();
        });
        owner.Call(() =>
        {
            Assert.True(owner.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Assert.DoesNotContain(giver.Issue.IssueQuest.JournalEntries, log => log.LogText.ToString() == "Stale result");
            Assert.True(giver.Issue.IssueQuest.IsOngoing);
            Assert.Single(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>());
        });
    }

    [Fact]
    public void ASecondBountyRequestFromTheSamePlayerIsRejectedWithoutCancelingTheFirst()
    {
        var fixture = CreateIssue();
        var second = CreateIssue();
        AcceptFromFirstClient(fixture);
        var client = environment.Clients.First();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(second.Giver, out var giver));
            MessageBroker.Instance.Publish(giver, new IssueConversationOpenedLocally(giver, "bounty-owner"));
            Campaign.Current.IssueManager.StartIssueQuest(giver);
        });
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(second.Giver, out var secondGiver));
            Assert.True(giver.Issue.IssueQuest.IsOngoing);
            Assert.True(secondGiver.Issue.IsOngoingWithoutQuest);
            Assert.Single(environment.Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        });
    }

    [Fact]
    public void LeavingTheGiverTracksTheHideoutOnlyForItsOwnerAndFinalizationRemovesIt()
    {
        var fixture = CreateIssue();
        AcceptFromFirstClient(fixture);
        string partyId = null;
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.Resolve<IPlayerManager>().TryGetPlayer("bounty-owner", out var player));
            partyId = player.MobilePartyId;
        });
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                using (new AllowedThread()) party.CurrentSettlement = giver.CurrentSettlement;
            });
        }
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            LeaveSettlementAction.ApplyForParty(party);
            Assert.Null(party.CurrentSettlement);
        });
        foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(fixture.Hideout, out var hideout));
                Assert.Equal(instance != environment.Clients.Last(), Campaign.Current.QuestManager.TrackedObjects.ContainsKey(hideout));
                if (instance != environment.Clients.Last())
                {
                    Assert.True(hideout.IsVisible);
                    Assert.True(hideout.Hideout.IsSpotted);
                }
            });
        }
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            giver.Issue.IssueQuest.CompleteQuestWithCancel();
        });
        foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(fixture.Hideout, out var hideout));
                Assert.False(Campaign.Current.QuestManager.TrackedObjects.ContainsKey(hideout));
            });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GiverDeathCancelsTheIssueAndRetainsDispatchedTroops(bool alternative)
    {
        var fixture = CreateIssue();
        if (alternative) AcceptFromFirstClient(fixture, alternative: true);
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Campaign.Current.IssueManager.OnHeroKilled(giver, null, default, false);
            Assert.Null(giver.Issue);
            var returns = environment.Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>();
            Assert.Equal(alternative, returns.TryGet("bounty-owner", out var troops));
            if (alternative) Assert.Equal(11, troops.TotalManCount);
        });
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                Assert.Null(giver.Issue);
                if (alternative && client == environment.Clients.First())
                    Assert.True(Assert.Single(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>()).IsEndedUnsuccessfully());
                else Assert.Empty(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>());
            });
        }
    }

    [Fact]
    public void JoiningPreservesTheLoadedQuestWhileARealHeirChangeCancelsOnlyItsOwnersQuest()
    {
        var fixture = CreateIssue();
        var otherFixture = CreateIssue();
        var playerId = AcceptFromFirstClient(fixture);
        AcceptFromFirstClient(otherFixture, clientIndex: 1);
        var client = environment.Clients.First();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            var quest = giver.Issue.IssueQuest;
            LeaveSettlementActionPatches.SuppressForPlayerSwitch = true;
            try
            {
                using (CallOriginalPolicy.AllowOriginalsForCurrentOperation())
                    Campaign.Current.QuestManager.OnPlayerCharacterChanged(giver, Hero.MainHero, MobileParty.MainParty, true);
            }
            finally
            {
                LeaveSettlementActionPatches.SuppressForPlayerSwitch = false;
            }
            Assert.True(quest.IsOngoing);
            Assert.Same(quest, giver.Issue.IssueQuest);
        });
        var heirId = environment.CreateRegisteredObject<Hero>();
        environment.Server.Call(() =>
        {
            var players = environment.Server.Resolve<IPlayerManager>();
            Assert.True(players.TryGetPlayer("bounty-owner", out var player));
            Assert.Equal(playerId, player.HeroId);
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(heirId, out var heir));
            Assert.True(players.ReplacePlayer(player, new Player(player.ControllerId, heirId, player.MobilePartyId, "", "")));
            environment.Server.Resolve<IMessageBroker>().Publish(null, new PlayerHeirSelectionCompleted(heir));
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(otherFixture.Giver, out var otherGiver));
            Assert.Null(giver.Issue);
            Assert.True(otherGiver.Issue.IssueQuest.IsOngoing);
        });
        client.Call(() => Assert.True(Assert.Single(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>()).IsEndedUnsuccessfully()));
        environment.Clients.Last().Call(() => Assert.False(Assert.Single(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>()).IsEnded()));
    }

    [Theory]
    [InlineData("success", 100, 3000, 0)]
    [InlineData("failure", -10, 0, 0)]
    [InlineData("timeout", -10, 0, 0)]
    [InlineData("success", 100, 3000, 1)]
    [InlineData("failure", -10, 0, 1)]
    [InlineData("timeout", -10, 0, 1)]
    [InlineData("success", 100, 3000, 2)]
    [InlineData("failure", -10, 0, 2)]
    [InlineData("timeout", -10, 0, 2)]
    public void TerminalConsequencesUseTheRecordedPlayerAndMirrorTheJournal(string outcome, int honorXp, int gold, int settlementCase)
    {
        var fixture = CreateIssue();
        foreach (var instance in new[] { environment.Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                using (new AllowedThread())
                {
                    giver.AddPower(100f - giver.Power);
                    giver.CurrentSettlement.Town.Security = 50f;
                }
            });
        }
        var playerId = AcceptFromFirstClient(fixture);
        var quests = new Dictionary<object, Quest>();
        foreach (var instance in new[] { environment.Server, environment.Clients.First() })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                quests.Add(instance, Assert.IsType<Quest>(giver.Issue.IssueQuest));
                Assert.Equal(giver.Issue.StringId + "_quest", giver.Issue.IssueQuest.StringId);
                Assert.Single(giver.Issue.IssueQuest.JournalEntries);
            });
        }
        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject<Hero>(playerId, out var player));
            var beforeGold = player.Gold;
            var previousHero = Hero.MainHero;
            var previousParty = Campaign.Current.MainParty;
            var campaignXp = Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor);
            var quest = quests[environment.Server];
            var town = quest.QuestGiver.CurrentSettlement.Town;
            var relation = quest.QuestGiver.GetRelation(player);
            if (settlementCase > 0)
            {
                Assert.True(environment.Server.ObjectManager.TryGetObject<Settlement>(fixture.Hideout, out var hideout));
                using (new AllowedThread()) quest.QuestGiver.StayingInSettlement = settlementCase == 1 ? null : hideout;
            }
            Assert.True(environment.Server.Resolve<IBountyHuntersQuestContext>().TryOpen(quest.QuestGiver, out var scope));
            using (scope)
            {
                if (outcome == "success")
                {
                    quest.AddLog(quest.SuccessQuestLogText);
                    quest.SuccessConsequences();
                }
                else if (outcome == "failure")
                {
                    quest.AddLog(quest.PlayerLostTheFightLogText);
                    quest.FailConsequences(false);
                }
                else quest.CompleteQuestWithTimeOut();
            }
            Assert.True(quest.IsFinalized);
            Assert.Null(quest.QuestGiver.Issue);
            Assert.Equal(beforeGold + gold, player.Gold);
            Assert.Equal(100f + (outcome == "success" ? 10f : -10f), quest.QuestGiver.Power);
            Assert.Equal(50f + (settlementCase > 0 ? 0f : outcome == "success" ? -5f : 5f), town.Security);
            Assert.Equal(relation + (outcome == "success" ? 5f : -5f), quest.QuestGiver.GetRelation(player));
            Assert.True(environment.Server.Resolve<PendingRegistry<PropertyOwner<PropertyObject>>>().TryGet(player, out var progress));
            Assert.Equal(honorXp, progress.GetPropertyValue(DefaultTraits.Honor));
            Assert.Equal(campaignXp, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
            Assert.Same(previousHero, Hero.MainHero);
            Assert.Same(previousParty, Campaign.Current.MainParty);
        });
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.Giver, out var giver));
                Assert.Equal(100f + (outcome == "success" ? 10f : -10f), giver.Power);
                Assert.Equal(50f + (settlementCase > 0 ? 0f : outcome == "success" ? -5f : 5f), giver.CurrentSettlement.Town.Security);
            });
        }
        var owner = environment.Clients.First();
        owner.Call(() =>
        {
            var quest = quests[owner];
            Assert.True(quest.IsFinalized);
            Assert.Equal(2, quest.JournalEntries.Count);
            var archive = Campaign.Current.LogEntryHistory.FindLastGameActionLog<JournalLogEntry>(entry => entry.IsRelatedTo(quest));
            Assert.NotNull(archive);
            Assert.True(archive.IsEnded());
            Assert.Equal(outcome != "success", archive.IsEndedUnsuccessfully());
        });
        environment.Clients.Last().Call(() => Assert.Empty(Campaign.Current.LogEntryHistory.GameActionLogs.OfType<JournalLogEntry>()));
    }
}
