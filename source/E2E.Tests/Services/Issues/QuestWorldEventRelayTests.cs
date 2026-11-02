using Common.Util;
using E2E.Tests.Environment;
using GameInterface.Services.Issues.Framework.Dispatch;
using GameInterface.Services.Issues.Framework.Interface;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>Checks shared world-event routing and cached absent listeners.</summary>
public class QuestWorldEventRelayTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;

    public QuestWorldEventRelayTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(environment);
    }

    public void Dispose() => environment.Dispose();

    [Theory]
    [InlineData(MapEvent.BattleTypes.IsForcingSupplies, 1)]
    [InlineData(MapEvent.BattleTypes.IsForcingVolunteers, 1)]
    [InlineData(MapEvent.BattleTypes.FieldBattle, 0)]
    [InlineData(MapEvent.BattleTypes.Raid, 0)]
    public void OnlyCoercionMapEventsReachTheQuest(MapEvent.BattleTypes battleType, int expected)
    {
        scenario.Server.Call(() =>
        {
            var quest = AttachListenerQuest();
            var mapEvent = (MapEvent)RuntimeHelpers.GetUninitializedObject(typeof(MapEvent));
            mapEvent._mapEventType = battleType;

            // Isolate the relay from unrelated dispatcher listeners that require a fully staged battle.
            typeof(QuestWorldEventRelayPatches).GetMethod("OnMapEventStartedPostfix", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, new object?[] { mapEvent, null, null });

            Assert.Equal(expected, quest.MapEvents);
        });
    }

    [Fact]
    public void HeroDeathRelayInvokesTheMatchingListenerOnceAsOwner()
    {
        scenario.Server.Call(() =>
        {
            var quest = AttachListenerQuest();
            typeof(QuestWorldEventRelayPatches).GetMethod("OnHeroKilledPostfix", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, new object?[] { quest.QuestGiver, null, KillCharacterAction.KillCharacterActionDetail.None, false });

            Assert.Equal(1, quest.Deaths);
        });
    }

    [Fact]
    public void MissingListenerIsRecordedAndSkippedOnSubsequentEvents()
    {
        scenario.Server.Call(() =>
        {
            AttachListenerQuest();
            var relay = scenario.Server.Resolve<IQuestEventRelay>();
            relay.Relay("AbsentListener", Type.EmptyTypes);
            relay.Relay("AbsentListener", Type.EmptyTypes);

            var missing = (ConcurrentDictionary<(Type, string), byte>)typeof(QuestEventRelay)
                .GetField("MissingListeners", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            Assert.True(missing.ContainsKey((typeof(ListenerQuest), "AbsentListener")));
        });
    }

    private ListenerQuest AttachListenerQuest()
    {
        Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
        Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var owner));
        Assert.True(scenario.Server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var party));
        using (new AllowedThread()) owner.PartyBelongedTo = party;
        var quest = new ListenerQuest(giver, owner);
        giver.Issue.IssueQuest = quest;
        Assert.True(scenario.Server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA));
        return quest;
    }

    /// <summary>Records the exact owner context in which shared listeners execute.</summary>
    private sealed class ListenerQuest : UnstartedQuest
    {
        private readonly Hero expectedOwner;
        public int Deaths { get; private set; }
        public int MapEvents { get; private set; }

        public ListenerQuest(Hero giver, Hero expectedOwner) : base("relay_quest", giver) => this.expectedOwner = expectedOwner;

        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            Assert.Same(expectedOwner, Hero.MainHero);
            Deaths++;
        }

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            Assert.Same(expectedOwner.PartyBelongedTo, MobileParty.MainParty);
            MapEvents++;
        }
    }
}
