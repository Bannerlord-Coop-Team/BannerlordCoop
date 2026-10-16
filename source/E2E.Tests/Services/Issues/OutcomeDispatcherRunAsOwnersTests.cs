using Common.Util;
using E2E.Tests.Environment;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class OutcomeDispatcherRunAsOwnersTests : IDisposable
{
    private readonly StubIssueQuestGenerator quests = new();
    private readonly QuestScenario scenario;

    private E2ETestEnvironment TestEnvironment { get; }

    public OutcomeDispatcherRunAsOwnersTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(TestEnvironment);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
        quests.Dispose();
    }

    [Fact]
    public void TheListenerRunsOnTheOwnedQuestAsItsOwner()
    {
        scenario.Accept(scenario.ClientA);
        var mirrorQuest = scenario.QuestOf(scenario.Server);

        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var owner));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(owner.HeroId, out var ownerHero));
            var calls = new List<(UnstartedQuest Quest, Hero MainHero)>();

            scenario.Server.Resolve<IOutcomeDispatcher>().RunAsOwners<UnstartedQuest>(quest => calls.Add((quest, Hero.MainHero)));

            var call = Assert.Single(calls);
            Assert.Same(mirrorQuest, call.Quest);
            Assert.Same(ownerHero, call.MainHero);
        });
    }

    [Fact]
    public void AQuestThatAlreadyEndedIsSkipped()
    {
        scenario.Accept(scenario.ClientA);
        var mirrorQuest = scenario.QuestOf(scenario.Server);

        scenario.Server.Call(() =>
        {
            using (new AllowedThread())
            {
                mirrorQuest._questState = QuestBase.QuestStates.Finalized;
            }

            var calls = 0;

            scenario.Server.Resolve<IOutcomeDispatcher>().RunAsOwners<UnstartedQuest>(_ => calls++);

            Assert.Equal(0, calls);
        });
    }
}
