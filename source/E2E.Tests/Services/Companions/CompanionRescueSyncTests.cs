using Common.Messaging;
using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Companions.Messages;
using GameInterface.Services.Companions.Patches;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Companions;

public class CompanionRescueSyncTests : IDisposable
{
    private static Hero oneToOneConversationHero;
    private readonly E2ETestEnvironment testEnvironment;
    private EnvironmentInstance Server => testEnvironment.Server;
    private IReadOnlyList<EnvironmentInstance> Clients => testEnvironment.Clients.ToList();

    public CompanionRescueSyncTests(ITestOutputHelper output)
    {
        testEnvironment = new E2ETestEnvironment(output);
    }

    [Fact]
    public void JoinPartyRescue_SentTwice_AddsCompanionOnceAndReturnsBothTerminalResults()
    {
        var context = CreateCaptiveCompanion();
        var requester = Clients[0];
        var countsAtCompletion = new List<int>();
        Server.NetworkSentMessages.Clear();
        Server.InternalMessages.Clear();
        requester.Resolve<IMessageBroker>().Subscribe<CompanionRescueCompletionReceived>(payload =>
        {
            if (payload.What.CompanionHeroId == context.HeroId &&
                payload.What.Kind == CompanionRescueRequestKind.JoinParty)
                countsAtCompletion.Add(GetTroopCount(requester, context.TargetPartyId, context.CharacterId));
        });

        requester.Call(() =>
        {
            Assert.True(requester.ObjectManager.TryGetObject<Hero>(context.HeroId, out var companion));
            Assert.True(requester.ObjectManager.TryGetObject<MobileParty>(context.TargetPartyId, out var targetParty));
            var rescue = new CompanionJoinedPartyByRescue(companion, targetParty);
            requester.Resolve<IMessageBroker>().Publish(this, rescue);
            requester.Resolve<IMessageBroker>().Publish(this, rescue);
            requester.Resolve<IMessageBroker>().Publish(this, new CompanionRescued(companion));
        });
        testEnvironment.FlushCoalescer();

        AssertTerminalPair(requester, context.HeroId, CompanionRescueRequestKind.JoinParty);
        Assert.Equal(new[] { 1, 1 }, countsAtCompletion);
        AssertSingleCaptivityRemoval(context);
        AssertSingleTargetAddition(context);
        AssertJoinPartyState(Server, context, assertHeroState: true);
        for (int i = 0; i < Clients.Count; i++)
            AssertJoinPartyState(Clients[i], context, assertHeroState: false);
        foreach (var otherClient in Clients.Skip(1))
        {
            Assert.DoesNotContain(otherClient.InternalMessages.OfType<CompanionRescueCompleted>(),
                message => message.CompanionHeroId == context.HeroId);
        }
    }

    [Fact]
    public void JoinPartyRescue_SameRequestReplayedAfterRecapture_ReturnsCachedResultWithoutRelease()
    {
        var context = CreateCaptiveCompanion();
        var requester = Clients[0];
        string requestId = Guid.NewGuid().ToString("N");
        var request = new DoCompanionJoinedPartyByRescue(
            context.HeroId,
            context.TargetPartyId,
            requestId,
            context.ClanId,
            context.CaptorPartyId);
        Server.NetworkSentMessages.Clear();
        Server.InternalMessages.Clear();

        requester.Call(() => requester.Resolve<INetwork>().SendAll(request));
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(context.HeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(context.CaptorPartyId, out var captor));
            TakePrisonerAction.Apply(captor, companion);
            Assert.True(companion.IsPrisoner);
            Assert.Same(captor, companion.PartyBelongedToAsPrisoner);
        });
        testEnvironment.FlushCoalescer();

        requester.Call(() => requester.Resolve<INetwork>().SendAll(request));

        var completions = requester.InternalMessages.OfType<CompanionRescueCompleted>()
            .Where(message => message.RequestId == requestId)
            .ToArray();
        Assert.Equal(2, completions.Length);
        Assert.All(completions, completion =>
        {
            Assert.Equal(CompanionRescueCompletionStatus.Accepted, completion.Status);
            Assert.Null(completion.Error);
        });
        AssertSingleCaptivityRemoval(context);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(context.HeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(context.CaptorPartyId, out var captor));
            Assert.True(companion.IsPrisoner);
            Assert.Same(captor, companion.PartyBelongedToAsPrisoner);
            Assert.Null(companion.PartyBelongedTo);
        });
        Assert.Equal(0, GetTroopCount(Server, context.TargetPartyId, context.CharacterId));
    }

    [Fact]
    public void LeadPartyRescue_SentTwice_CreatesOnePartyAndReturnsBothTerminalResults()
    {
        var context = CreateCaptiveCompanion();
        var requester = Clients[0];
        Server.NetworkSentMessages.Clear();
        Server.InternalMessages.Clear();

        requester.Call(() =>
        {
            Assert.True(requester.ObjectManager.TryGetObject<Hero>(context.HeroId, out var companion));
            Assert.True(requester.ObjectManager.TryGetObject<MobileParty>(context.TargetPartyId, out var targetParty));
            var members = TroopRoster.CreateDummyTroopRoster();
            members.AddToCounts(companion.CharacterObject, 1);
            var prisoners = TroopRoster.CreateDummyTroopRoster();
            var rescue = new PartyScreenClosedFromRescuing(members, prisoners, targetParty.Party);
            requester.Resolve<IMessageBroker>().Publish(this, rescue);
            requester.Resolve<IMessageBroker>().Publish(this, rescue);
        });
        testEnvironment.FlushCoalescer();

        AssertTerminalPair(requester, context.HeroId, CompanionRescueRequestKind.LeadParty);
        AssertSingleCaptivityRemoval(context);
        Assert.Single(Server.NetworkSentMessages.OfType<NetworkAddWarParty>());
        AssertLeadPartyState(Server, context, assertHeroState: true);
        foreach (var client in Clients)
            AssertLeadPartyState(client, context, assertHeroState: false);
        foreach (var otherClient in Clients.Skip(1))
        {
            Assert.DoesNotContain(otherClient.InternalMessages.OfType<CompanionRescueCompleted>(),
                message => message.CompanionHeroId == context.HeroId);
        }
    }

    [Fact]
    public void LeadPartyRescue_SameRequestSentTwice_ReturnsCachedAcceptedResult()
    {
        var context = CreateCaptiveCompanion();
        var requester = Clients[0];
        requester.NetworkSentMessages.Clear();
        Server.NetworkSentMessages.Clear();
        Server.InternalMessages.Clear();

        requester.Call(() =>
        {
            Assert.True(requester.ObjectManager.TryGetObject<Hero>(context.HeroId, out var companion));
            Assert.True(requester.ObjectManager.TryGetObject<MobileParty>(context.TargetPartyId, out var targetParty));
            var members = TroopRoster.CreateDummyTroopRoster();
            members.AddToCounts(companion.CharacterObject, 1);
            var prisoners = TroopRoster.CreateDummyTroopRoster();
            requester.Resolve<IMessageBroker>().Publish(this,
                new PartyScreenClosedFromRescuing(members, prisoners, targetParty.Party));
        });

        var request = Assert.Single(requester.NetworkSentMessages
            .OfType<DoPartyScreenClosedFromRescuing>());
        requester.Call(() => requester.Resolve<INetwork>().SendAll(request));
        testEnvironment.FlushCoalescer();

        var completions = requester.InternalMessages.OfType<CompanionRescueCompleted>()
            .Where(message => message.RequestId == request.RequestId)
            .ToArray();
        Assert.Equal(2, completions.Length);
        Assert.All(completions, completion =>
        {
            Assert.Equal(CompanionRescueCompletionStatus.Accepted, completion.Status);
            Assert.Null(completion.Error);
        });
        AssertSingleCaptivityRemoval(context);
        Assert.Single(Server.NetworkSentMessages.OfType<NetworkAddWarParty>());
        AssertLeadPartyState(Server, context, assertHeroState: true);
        foreach (var client in Clients)
            AssertLeadPartyState(client, context, assertHeroState: false);
    }

    [Fact]
    public void JoinPartyRescue_WithStaleClan_UiCompletionReturnsRejectionWithoutRelease()
    {
        var context = CreateCaptiveCompanion();
        string staleClanId = testEnvironment.CreateRegisteredObject<Clan>();
        testEnvironment.FlushCoalescer();
        var requester = Clients[0];
        requester.NetworkSentMessages.Clear();
        var harmony = new Harmony($"{nameof(CompanionRescueSyncTests)}.{Guid.NewGuid():N}");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(context.HeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(staleClanId, out var staleClan));
            companion._companionOf = staleClan;
        });

        try
        {
            requester.Call(() =>
            {
                Assert.True(requester.ObjectManager.TryGetObject<Hero>(context.HeroId, out var companion));
                Assert.True(requester.ObjectManager.TryGetObject<MobileParty>(context.TargetPartyId, out var targetParty));
                oneToOneConversationHero = companion;
                var previousMainParty = Campaign.Current.MainParty;
                Campaign.Current.MainParty = targetParty;
                try
                {
                    harmony.Patch(
                        AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.OneToOneConversationHero)),
                        prefix: new HarmonyMethod(AccessTools.Method(
                            typeof(CompanionRescueSyncTests), nameof(GetOneToOneConversationHeroPrefix))));
                    var behavior = new CompanionRolesCampaignBehavior();

                    Assert.False(CompanionRolesPatches
                        .CompanionRescueAnswerOptionsJoinPartyConsequencePrefix(ref behavior));
                    Assert.True(behavior._partyCreatedAfterRescueForCompanion);
                    Assert.False(CompanionRolesPatches.EndRescueCompanionPrefix(ref behavior));
                    Assert.False(behavior._partyCreatedAfterRescueForCompanion);
                }
                finally
                {
                    Campaign.Current.MainParty = previousMainParty;
                }
            });
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            oneToOneConversationHero = null;
        }

        var request = Assert.Single(requester.NetworkSentMessages
            .OfType<DoCompanionJoinedPartyByRescue>());
        Assert.Equal(context.ClanId, request.ExpectedClanId);
        Assert.Empty(requester.NetworkSentMessages.OfType<RescueCompanion>());

        var completion = requester.InternalMessages.OfType<CompanionRescueCompleted>()
            .Single(message => message.RequestId == request.RequestId);
        Assert.Equal(CompanionRescueCompletionStatus.Rejected, completion.Status);
        Assert.Contains("owning clan changed", completion.Error);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(context.HeroId, out var companion));
            Assert.True(companion.IsPrisoner);
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(context.CaptorPartyId, out var captor));
            Assert.Same(captor, companion.PartyBelongedToAsPrisoner);
        });
        Assert.Equal(0, GetTroopCount(Server, context.TargetPartyId, context.CharacterId));
    }

    private static bool GetOneToOneConversationHeroPrefix(ref Hero __result)
    {
        __result = oneToOneConversationHero;
        return false;
    }

    private RescueContext CreateCaptiveCompanion()
    {
        string targetPartyId = null;
        string clanId = null;
        string captorPartyId = null;
        string heroId = null;
        string characterId = null;
        int originalWarPartyCount = 0;

        Server.Call(() =>
        {
            var targetParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var captorParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var clan = targetParty.ActualClan;
            var companion = GameObjectCreator.CreateInitializedObject<Hero>();
            var companionName = new TextObject("Rescue Idempotency Test Companion");
            companion.SetName(companionName, companionName);
            companion.Clan = null;
            companion.SetNewOccupation(Occupation.Wanderer);
            AddCompanionAction.Apply(clan, companion);

            int partyGoldLowerThreshold = Campaign.Current.Models.ClanFinanceModel.PartyGoldLowerThreshold;
            if (companion.Gold < partyGoldLowerThreshold)
            {
                GiveGoldAction.ApplyBetweenCharacters(
                    null, companion, partyGoldLowerThreshold - companion.Gold, false);
            }
            TakePrisonerAction.Apply(captorParty.Party, companion);

            Assert.True(companion.IsPrisoner);
            Assert.Same(captorParty.Party, companion.PartyBelongedToAsPrisoner);
            Assert.Null(companion.PartyBelongedTo);
            Assert.True(Server.ObjectManager.TryGetId(targetParty, out targetPartyId));
            Assert.True(Server.ObjectManager.TryGetId(clan, out clanId));
            Assert.True(Server.ObjectManager.TryGetId(captorParty.Party, out captorPartyId));
            Assert.True(Server.ObjectManager.TryGetId(companion, out heroId));
            Assert.True(Server.ObjectManager.TryGetId(companion.CharacterObject, out characterId));
            originalWarPartyCount = clan.WarPartyComponents.Count;
        });
        testEnvironment.FlushCoalescer();

        return new RescueContext(targetPartyId, clanId, captorPartyId,
            heroId, characterId, originalWarPartyCount);
    }

    private static void AssertTerminalPair(EnvironmentInstance requester, string heroId,
        CompanionRescueRequestKind kind)
    {
        var completions = requester.InternalMessages.OfType<CompanionRescueCompleted>()
            .Where(message => message.CompanionHeroId == heroId && message.Kind == kind)
            .ToArray();
        Assert.Equal(2, completions.Length);
        Assert.Equal(CompanionRescueCompletionStatus.Accepted, completions[0].Status);
        Assert.Equal(CompanionRescueCompletionStatus.AlreadyCompleted, completions[1].Status);
        Assert.All(completions, completion => Assert.Null(completion.Error));
    }

    private static void AssertJoinPartyState(EnvironmentInstance instance, RescueContext context,
        bool assertHeroState)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(context.HeroId, out var companion));
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(context.TargetPartyId, out var targetParty));
            Assert.False(companion.IsPrisoner);
            if (assertHeroState)
                Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);
            Assert.Same(targetParty, companion.PartyBelongedTo);
            Assert.Equal(1, targetParty.MemberRoster.GetTroopCount(companion.CharacterObject));
        });
    }

    private static void AssertLeadPartyState(EnvironmentInstance instance, RescueContext context,
        bool assertHeroState)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(context.HeroId, out var companion));
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(context.TargetPartyId, out var targetParty));
            Assert.True(instance.ObjectManager.TryGetObject<Clan>(context.ClanId, out var clan));
            var ledParties = clan.WarPartyComponents
                .Select(component => component?.MobileParty)
                .Where(party => party != null && party.LeaderHero == companion)
                .ToArray();
            Assert.False(companion.IsPrisoner);
            if (assertHeroState)
                Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);
            Assert.Equal(0, targetParty.MemberRoster.GetTroopCount(companion.CharacterObject));
            Assert.Single(ledParties);
            Assert.Same(ledParties[0], companion.PartyBelongedTo);
            Assert.Equal(context.OriginalWarPartyCount + 1, clan.WarPartyComponents.Count);
        });
    }

    private static int GetTroopCount(EnvironmentInstance instance, string partyId, string characterId)
    {
        Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
        Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));
        return party.MemberRoster.GetTroopCount(character);
    }

    private void AssertSingleCaptivityRemoval(RescueContext context)
    {
        Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(context.CaptorPartyId, out var captor));
        Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(context.CharacterId, out var character));
        Assert.Single(Server.InternalMessages.OfType<CountsAtIndexAdded>(),
            message => message.TroopRoster == captor.PrisonRoster &&
                message.Character == character && message.CountChange == -1);
    }

    private void AssertSingleTargetAddition(RescueContext context)
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(context.TargetPartyId, out var targetParty));
        Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(context.CharacterId, out var character));
        Assert.Single(Server.InternalMessages.OfType<CountsAtIndexAdded>(),
            message => message.TroopRoster == targetParty.MemberRoster &&
                message.Character == character && message.CountChange == 1);
    }

    private readonly record struct RescueContext(
        string TargetPartyId,
        string ClanId,
        string CaptorPartyId,
        string HeroId,
        string CharacterId,
        int OriginalWarPartyCount);

    public void Dispose() => testEnvironment.Dispose();
}
