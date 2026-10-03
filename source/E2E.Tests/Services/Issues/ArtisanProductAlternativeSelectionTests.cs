using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Handlers;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Localization;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

using Issue = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;

public sealed class ArtisanProductAlternativeSelectionTests : IDisposable
{
    private static Hero conversationHero;
    private readonly E2ETestEnvironment environment;
    private readonly Harmony harmony = new($"artisan-selection-{Guid.NewGuid():N}");
    private EnvironmentInstance Client => environment.Clients.Single();

    public ArtisanProductAlternativeSelectionTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output, numClients: 1);
        harmony.Patch(AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.OneToOneConversationHero)),
            prefix: new HarmonyMethod(typeof(ArtisanProductAlternativeSelectionTests), nameof(ConversationHero)));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(8)]
    public void DoneKeepsSelectedIssueRosterWithoutCommittingPartyOrGold(int count)
    {
        var fixture = Open();
        Client.Call(() =>
        {
            Transfer(fixture, count);
            Assert.True(fixture.Logic.DoneLogic(false));
            Assert.Equal(count, fixture.Issue.AlternativeSolutionSentTroops.GetTroopCount(fixture.Troop));
            Assert.Equal(1, fixture.Issue.AlternativeSolutionSentTroops.GetTroopCount(fixture.Companion));
            Assert.Equal(10, fixture.Party.MemberRoster.GetTroopCount(fixture.Troop));
            Assert.Equal(1, fixture.Party.MemberRoster.GetTroopCount(fixture.Companion));
            Assert.Equal(fixture.Gold, Hero.MainHero.Gold);
        });
        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkCompleteDoneLogic>());
        Assert.Empty(Client.InternalMessages.GetMessages<PartyDoneLogicAttempted>());
    }

    [Fact]
    public void CancelDoesNotRestoreOverAuthoritativePartyChanges()
    {
        var fixture = Open();
        Client.Call(() =>
        {
            Transfer(fixture, 6);
            using (new AllowedThread()) fixture.Party.MemberRoster.AddToCounts(fixture.Troop, 3);
            fixture.Logic.Reset(true);
            fixture.Logic.OnPartyScreenClosed(true);
            Assert.Equal(13, fixture.Party.MemberRoster.GetTroopCount(fixture.Troop));
            Assert.Equal(1, fixture.Party.MemberRoster.GetTroopCount(fixture.Companion));
            Assert.Empty(fixture.Issue.AlternativeSolutionSentTroops.GetTroopRoster());
            Assert.Equal(fixture.Gold, Hero.MainHero.Gold);
        });
        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkCompleteDoneLogic>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LosingScreenCannotOverwriteOrReturnAnotherPlayersAcceptedTroops(bool cancel)
    {
        var fixture = Open();
        Client.Call(() =>
        {
            Transfer(fixture, 6);
            using (new AllowedThread())
            {
                fixture.Issue.AlternativeSolutionSentTroops.AddToCounts(fixture.Troop, 2);
                fixture.Issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
            }
            if (cancel) fixture.Logic.Reset(true);
            else Assert.True(fixture.Logic.DoneLogic(false));
            fixture.Logic.OnPartyScreenClosed(cancel);
            AccessTools.Method(typeof(TaleWorlds.CampaignSystem.CampaignBehaviors.IssuesCampaignBehavior),
                "issue_offer_player_accept_alternative_5_b_consequence").Invoke(null, null);
            Assert.Equal(2, fixture.Issue.AlternativeSolutionSentTroops.GetTroopCount(fixture.Troop));
            Assert.Equal(10, fixture.Party.MemberRoster.GetTroopCount(fixture.Troop));
            Assert.Equal(1, fixture.Party.MemberRoster.GetTroopCount(fixture.Companion));
        });
    }

    [Fact]
    public void SendingClaimClearsOnlyTemporarySelectionAndLeavesLocalPartyWhole()
    {
        var fixture = Open();
        Client.Call(() =>
        {
            Transfer(fixture, 6);
            Assert.True(fixture.Logic.DoneLogic(false));
            fixture.Issue.StartIssueWithAlternativeSolution();
            Assert.Empty(fixture.Issue.AlternativeSolutionSentTroops.GetTroopRoster());
            Assert.Equal(10, fixture.Party.MemberRoster.GetTroopCount(fixture.Troop));
            Assert.Equal(1, fixture.Party.MemberRoster.GetTroopCount(fixture.Companion));
        });
        Assert.Single(Client.NetworkSentMessages.GetMessages<RequestQuestTypeAcceptAlternative>());
        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkCompleteDoneLogic>());
    }

    [Fact]
    public void ServerWagesMatchActualScreenCostBeforeTheIssueHasACompanion()
    {
        var fixture = Open();
        Client.Call(() =>
        {
            Transfer(fixture, 6);
            Assert.Null(fixture.Issue.AlternativeSolutionHero);
            int wages = AlternativeSolutionStartRunner.GetArtisanAlternativeWages(
                fixture.Issue, fixture.Logic.MemberRosters[0], fixture.Companion.HeroObject);
            Assert.True(wages > 0);
            Assert.Equal(-fixture.Logic.CurrentData.PartyGoldChangeAmount, wages);
        });
    }

    [Fact]
    public void FailedAuthoritativeStartRestoresOnlyRequestersGoldAndTroops()
    {
        var giverId = environment.CreateRegisteredObject<Hero>();
        var ownerId = environment.CreateRegisteredObject<Hero>();
        var observerId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        var targetId = environment.CreateRegisteredObject<CharacterObject>();
        XpCapModels.Install(environment.Server);
        environment.Server.Call(() =>
        {
            var server = environment.Server;
            Assert.True(server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(observerId, out var observer));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            Assert.True(server.ObjectManager.TryGetObject<CharacterObject>(targetId, out var target));
            var issue = ObjectHelper.SkipConstructor<Issue>();
            issue._issueOwner = giver;
            issue._issueState = IssueBase.IssueState.Ongoing;
            AccessTools.Field(typeof(IssueBase), "_journalEntries").SetValue(issue, new MBList<JournalLog>());
            AccessTools.Field(typeof(IssueBase), nameof(IssueBase.AlternativeSolutionSentTroops))
                .SetValue(issue, TroopRoster.CreateDummyTroopRoster());
            var selection = TroopRoster.CreateDummyTroopRoster();
            using (new AllowedThread())
            {
                giver.Issue = issue;
                owner.Gold = 1000;
                observer.Gold = 2000;
                troop.Level = 6;
                target.Level = 11;
                troop.UpgradeTargets = new[] { target };
                party.MemberRoster.AddToCounts(troop, 10, xpChange: 1500);
                selection.AddToCounts(troop, 6, xpChange: 300);
            }
            var player = new Player("artisan-rollback-player", ownerId, partyId, "", "");
            var rollback = (Action)AccessTools.Method(typeof(GenericQuestTypeAcceptHandler), "CaptureAlternativeRollback")
                .Invoke(server.Resolve<GenericQuestTypeAcceptHandler>(), new object[] { giver, player, selection });
            GiveGoldAction.ApplyBetweenCharacters(null, owner, -180);
            party.MemberRoster.AddToCounts(troop, -6, xpChange: -300);
            issue.AlternativeSolutionSentTroops.Add(selection);
            rollback();
            Assert.Equal(1000, owner.Gold);
            Assert.Equal(2000, observer.Gold);
            Assert.Equal(10, party.MemberRoster.GetTroopCount(troop));
            Assert.Equal(1500, party.MemberRoster.GetElementCopyAtIndex(party.MemberRoster.FindIndexOfTroop(troop)).Xp);
            Assert.Empty(issue.AlternativeSolutionSentTroops.GetTroopRoster());
        });
    }

    [Theory]
    [InlineData(6, 60, 0)]
    [InlineData(6, 1500, 300)]
    [InlineData(10, 60, 60)]
    [InlineData(10, 3000, 3000)]
    public void ArtisanAcceptanceConservesAuthoritativeXpAcrossSelectedAndRemainingTroops(int count, int xp, int sentXp)
    {
        var ownerId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        var targetId = environment.CreateRegisteredObject<CharacterObject>();
        XpCapModels.Install(environment.Server);
        environment.Server.Call(() =>
        {
            var server = environment.Server;
            Assert.True(server.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            Assert.True(server.ObjectManager.TryGetObject<CharacterObject>(targetId, out var target));
            var claim = TroopRoster.CreateDummyTroopRoster();
            using (new AllowedThread())
            {
                troop.Level = 6;
                target.Level = 11;
                troop.UpgradeTargets = new[] { target };
                party.MemberRoster.AddToCounts(troop, 10, xpChange: xp);
                claim.AddToCounts(troop, count, xpChange: 500000);
            }
            Assert.Equal(300, Campaign.Current.Models.PartyTroopUpgradeModel.GetXpCostForUpgrade(party.Party, troop, target));
            var player = new Player("artisan-xp-player", ownerId, partyId, "", "");
            var packed = server.Resolve<ITroopRosterInterface>().PackTroopRosterData(claim);
            var validated = (TroopRoster)AccessTools.Method(typeof(GenericQuestTypeAcceptHandler), "BuildValidatedSentTroops")
                .Invoke(server.Resolve<GenericQuestTypeAcceptHandler>(), new object[] { player, packed });
            using (new MainHeroSubstitutionScope(owner, party))
                AccessTools.Method(typeof(AlternativeSolutionStartRunner), "RemoveFromTrueOwnerParty")
                    .Invoke(null, new object[] { validated, true });

            var sent = Assert.Single(validated.GetTroopRoster());
            var remainingIndex = party.MemberRoster.FindIndexOfTroop(troop);
            var remainingXp = remainingIndex < 0 ? 0 : party.MemberRoster.GetElementCopyAtIndex(remainingIndex).Xp;
            Assert.Equal(count, sent.Number);
            Assert.Equal(sentXp, sent.Xp);
            Assert.Equal(10 - count, party.MemberRoster.GetTroopCount(troop));
            Assert.Equal(xp, sent.Xp + remainingXp);
        });
    }

    private Fixture Open()
    {
        var giverId = environment.CreateRegisteredObject<Hero>();
        var companionId = environment.CreateRegisteredObject<Hero>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        Fixture fixture = null;
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            conversationHero = giver;
            var party = MobileParty.MainParty;
            var issue = ObjectHelper.SkipConstructor<Issue>();
            issue._issueOwner = giver;
            issue._issueState = IssueBase.IssueState.Ongoing;
            AccessTools.Field(typeof(IssueBase), nameof(IssueBase.AlternativeSolutionSentTroops))
                .SetValue(issue, TroopRoster.CreateDummyTroopRoster());
            Campaign.Current.IssueManager._issues[giver] = issue;
            using (new AllowedThread())
            {
                giver.Issue = issue;
                Hero.MainHero.Gold = 100000;
                troop.UpgradeTargets = Array.Empty<CharacterObject>();
                party.MemberRoster.AddToCounts(troop, 10);
                issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
            }
            var logic = new PartyScreenLogic();
            var data = new PartyScreenLogicInitializationData
            {
                LeftMemberRoster = issue.AlternativeSolutionSentTroops,
                LeftPrisonerRoster = TroopRoster.CreateDummyTroopRoster(),
                RightMemberRoster = party.MemberRoster,
                RightPrisonerRoster = party.PrisonRoster,
                RightOwnerParty = party.Party,
                LeftPartyName = new TextObject("selection"),
                RightPartyName = new TextObject("party"),
                MemberTransferState = PartyScreenLogic.TransferState.Transferable,
                PrisonerTransferState = PartyScreenLogic.TransferState.NotTransferable,
                PartyPresentationDoneButtonDelegate = (_, _, _, _, _, _, _, _, _) => true,
                TroopTransferableDelegate = (_, _, _, _) => true,
                QuestModeWageDaysMultiplier = issue.GetTotalAlternativeSolutionDurationInDays(),
            };
            Assert.False(Common.ModInformation.IsServer);
            Assert.Same(issue, TaleWorlds.CampaignSystem.CampaignBehaviors.IssuesCampaignBehavior.GetIssueOwnersIssue());
            Assert.Same(data.RightOwnerParty, PartyBase.MainParty);
            logic.Initialize(data);
            Assert.NotSame(issue.AlternativeSolutionSentTroops, logic.MemberRosters[0]);
            Assert.NotSame(party.MemberRoster, logic.MemberRosters[1]);
            fixture = new Fixture(issue, logic, party, troop, companion.CharacterObject, Hero.MainHero.Gold);
        });
        return fixture;
    }

    private static void Transfer(Fixture fixture, int count)
    {
        var command = new PartyScreenLogic.PartyCommand();
        command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right,
            PartyScreenLogic.TroopType.Member, fixture.Troop, count, 0, -1);
        using (new AllowedThread()) fixture.Logic.TransferTroop(command, false);
        Assert.Equal(count, fixture.Logic.MemberRosters[0].GetTroopCount(fixture.Troop));
    }

    private static bool ConversationHero(ref Hero __result)
    {
        __result = conversationHero;
        return false;
    }

    public void Dispose()
    {
        harmony.UnpatchAll(harmony.Id);
        conversationHero = null;
        environment.Dispose();
    }

    private record Fixture(Issue Issue, PartyScreenLogic Logic, MobileParty Party,
        CharacterObject Troop, CharacterObject Companion, int Gold);
}
