using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Companions.Messages;
using GameInterface.Services.MapEvents.Interfaces;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Companions;

public class CompanionDismissalSyncTests : IDisposable
{
    private static readonly System.Reflection.MethodBase CreateObituaryMethod =
        AccessTools.Method(typeof(KillCharacterAction), "CreateObituary");

    private readonly E2ETestEnvironment testEnvironment;
    private EnvironmentInstance Server => testEnvironment.Server;
    private IReadOnlyList<EnvironmentInstance> Clients => testEnvironment.Clients.ToList();

    public CompanionDismissalSyncTests(ITestOutputHelper output)
    {
        testEnvironment = new E2ETestEnvironment(output);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(6, 1)]
    public void CompanionFired_RemovesEveryCopyBeforeRequesterCompletion(
        int initialCount, int woundedCount)
    {
        var context = CreateCompanion(initialCount, woundedCount, includeParty: true);
        var requester = Clients[0];
        CompanionDismissalCompleted? completion = null;
        int? countAtCompletion = null;
        int completionCount = 0;

        requester.Resolve<IMessageBroker>().Subscribe<CompanionDismissalCompleted>(payload =>
        {
            completion = payload.What;
            completionCount++;
            countAtCompletion = GetTroopCount(requester, context.PartyId, context.CharacterId);
        });

        Server.NetworkSentMessages.Clear();
        requester.Call(() =>
            {
                Assert.True(requester.ObjectManager.TryGetObject<Hero>(context.HeroId, out var companion));
                requester.Resolve<IMessageBroker>().Publish(this, new CompanionFired(companion));
            },
            new[] { CreateObituaryMethod });
        testEnvironment.FlushCoalescer();

        Assert.NotNull(completion);
        Assert.True(completion.Value.Success, completion.Value.Error);
        Assert.Equal(context.HeroId, completion.Value.OneToOneConversationHeroId);
        Assert.Equal(0, countAtCompletion);
        Assert.Equal(0, GetTroopCount(Server, context.PartyId, context.CharacterId));
        foreach (var client in Clients)
        {
            Assert.Equal(0, GetTroopCount(client, context.PartyId, context.CharacterId));
        }

        var orderedTail = Server.NetworkSentMessages
            .Where(message => message is NetworkTroopRosterSetNumber
                or NetworkTroopRosterRemoveZeroCounts
                or FireCompanionCompleted)
            .TakeLast(3)
            .ToArray();
        Assert.IsType<NetworkTroopRosterSetNumber>(orderedTail[0]);
        Assert.IsType<NetworkTroopRosterRemoveZeroCounts>(orderedTail[1]);
        Assert.IsType<FireCompanionCompleted>(orderedTail[2]);

        Server.Resolve<INetwork>().Send(requester.NetPeer,
            new FireCompanionCompleted(completion.Value.RequestId, context.HeroId, true, null));
        Assert.Equal(1, completionCount);

        foreach (var otherClient in Clients.Skip(1))
        {
            Assert.Empty(otherClient.InternalMessages.OfType<FireCompanionCompleted>());
        }
    }

    [Fact]
    public void BareDismissalEncounter_BeginUpdateYieldsWithoutFinishing()
    {
        var requester = Clients[0];
        requester.Call(() =>
        {
            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            Campaign.Current.PlayerEncounter = encounter;

            new PlayerEncounterInterface().UpdateInternalAfterBattle(encounter);

            Assert.Same(encounter, PlayerEncounter.Current);
            Assert.True(encounter._stateHandled);
            Assert.False(PlayerEncounter.LeaveEncounter);
            Campaign.Current.PlayerEncounter = null;
        });
    }

    [Fact]
    public void CompanionFired_WithoutOwningClan_ReturnsLocalTerminalFailure()
    {
        string heroId = null;
        Server.Call(() =>
        {
            var companion = GameObjectCreator.CreateInitializedObject<Hero>();
            var companionName = new TextObject("Unowned Dismissal Test Companion");
            companion.SetName(companionName, companionName);
            Assert.True(Server.ObjectManager.TryGetId(companion, out heroId));
        });
        testEnvironment.FlushCoalescer();

        var requester = Clients[0];
        CompanionDismissalCompleted? completion = null;
        requester.Resolve<IMessageBroker>().Subscribe<CompanionDismissalCompleted>(payload => completion = payload.What);

        requester.Call(() =>
        {
            Assert.True(requester.ObjectManager.TryGetObject<Hero>(heroId, out var companion));
            requester.Resolve<IMessageBroker>().Publish(this, new CompanionFired(companion));
        });

        Assert.NotNull(completion);
        Assert.False(completion.Value.Success);
        Assert.Equal(heroId, completion.Value.OneToOneConversationHeroId);
        Assert.Contains("no owning clan", completion.Value.Error);
        Assert.Empty(Server.InternalMessages.OfType<FireCompanion>());
    }

    [Fact]
    public void FireCompanion_WithStaleOwnership_ReturnsFailureWithoutMutatingCompanion()
    {
        var context = CreateCompanion(1, 0, includeParty: true);
        string staleClanId = null;
        Server.Call(() =>
        {
            var staleClan = GameObjectCreator.CreateInitializedObject<Clan>();
            Assert.True(Server.ObjectManager.TryGetId(staleClan, out staleClanId));
        });
        testEnvironment.FlushCoalescer();

        var requester = Clients[0];
        string requestId = Guid.NewGuid().ToString("N");
        requester.Call(() => requester.Resolve<INetwork>().SendAll(new FireCompanion(
            requestId, context.HeroId, staleClanId, context.PartyId)));

        var completion = requester.InternalMessages.OfType<FireCompanionCompleted>()
            .Single(message => message.RequestId == requestId);
        Assert.False(completion.Success);
        Assert.Contains("clan changed", completion.Error);
        Assert.Equal(1, GetTroopCount(Server, context.PartyId, context.CharacterId));
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(context.HeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(context.ClanId, out var clan));
            Assert.Same(clan, companion.CompanionOf);
        });
    }

    [Fact]
    public void CompanionFired_WithoutParty_CompletesSuccessfully()
    {
        var context = CreateCompanion(0, 0, includeParty: false);
        var requester = Clients[0];
        CompanionDismissalCompleted? completion = null;
        requester.Resolve<IMessageBroker>().Subscribe<CompanionDismissalCompleted>(payload => completion = payload.What);

        requester.Call(() =>
            {
                Assert.True(requester.ObjectManager.TryGetObject<Hero>(context.HeroId, out var companion));
                requester.Resolve<IMessageBroker>().Publish(this, new CompanionFired(companion));
            },
            new[] { CreateObituaryMethod });

        Assert.NotNull(completion);
        Assert.True(completion.Value.Success, completion.Value.Error);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(context.HeroId, out var companion));
            Assert.Null(companion.CompanionOf);
        });
    }

    [Fact]
    public void CompanionFired_WhenPostRemovalActionThrows_ReturnsTerminalFailureAfterCorrection()
    {
        var context = CreateCompanion(1, 0, includeParty: true);
        var requester = Clients[0];
        CompanionDismissalCompleted? completion = null;
        requester.Resolve<IMessageBroker>().Subscribe<CompanionDismissalCompleted>(payload => completion = payload.What);

        // The stripped E2E campaign intentionally has no encyclopedia pages, so vanilla obituary
        // creation throws after RemoveCompanionAction. This exercises the handler's terminal-error path.
        requester.Call(() =>
        {
            Assert.True(requester.ObjectManager.TryGetObject<Hero>(context.HeroId, out var companion));
            requester.Resolve<IMessageBroker>().Publish(this, new CompanionFired(companion));
        });

        Assert.NotNull(completion);
        Assert.False(completion.Value.Success);
        Assert.NotEmpty(completion.Value.Error);
        Assert.Equal(0, GetTroopCount(Server, context.PartyId, context.CharacterId));
        foreach (var client in Clients)
        {
            Assert.Equal(0, GetTroopCount(client, context.PartyId, context.CharacterId));
        }
    }

    private CompanionContext CreateCompanion(int initialCount, int woundedCount, bool includeParty)
    {
        string partyId = null;
        string clanId = null;
        string heroId = null;
        string characterId = null;

        Server.Call(() =>
        {
            var clan = GameObjectCreator.CreateInitializedObject<Clan>();
            var companion = GameObjectCreator.CreateInitializedObject<Hero>();
            MobileParty party = null;

            var companionName = new TextObject("Dismissal Test Companion");
            companion.SetName(companionName, companionName);

            AddCompanionAction.Apply(clan, companion);
            if (includeParty)
            {
                party = GameObjectCreator.CreateInitializedObject<MobileParty>();
                AddHeroToPartyAction.Apply(companion, party, true);
                int currentCount = party.MemberRoster.GetTroopCount(companion.CharacterObject);
                party.MemberRoster.AddToCounts(companion.CharacterObject, initialCount - currentCount);
                int companionIndex = party.MemberRoster.FindIndexOfTroop(companion.CharacterObject);
                party.MemberRoster.SetElementWoundedNumber(companionIndex, woundedCount);
                Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
            }

            Assert.True(Server.ObjectManager.TryGetId(clan, out clanId));
            Assert.True(Server.ObjectManager.TryGetId(companion, out heroId));
            Assert.True(Server.ObjectManager.TryGetId(companion.CharacterObject, out characterId));
        });
        testEnvironment.FlushCoalescer();

        return new CompanionContext(partyId, clanId, heroId, characterId);
    }

    private static int GetTroopCount(EnvironmentInstance instance, string partyId, string characterId)
    {
        Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
        Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));
        return party.MemberRoster.GetTroopCount(character);
    }

    private readonly record struct CompanionContext(
        string PartyId, string ClanId, string HeroId, string CharacterId);

    public void Dispose() => testEnvironment.Dispose();
}
