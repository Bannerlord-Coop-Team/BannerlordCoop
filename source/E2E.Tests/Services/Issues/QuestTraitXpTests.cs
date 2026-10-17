using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Rewards;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class QuestTraitXpTests : IDisposable
{
    private const int Reward = 100;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;

    public QuestTraitXpTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void QuestRewards_OnTheServerLeaveTheSharedTraitStoreAndEveryOwnersLevelAlone()
    {
        Server.Call(() =>
        {
            var store = new PropertyOwner<PropertyObject>();
            Campaign.Current.PlayerTraitDeveloper = store;

            var first = GameObjectCreator.CreateInitializedObject<Hero>();
            var second = GameObjectCreator.CreateInitializedObject<Hero>();

            using (new AllowedThread())
            {
                first.SetTraitLevel(DefaultTraits.Calculating, 1);
                second.SetTraitLevel(DefaultTraits.Calculating, 0);
            }

            var calculating = new[] { new Tuple<TraitObject, int>(DefaultTraits.Calculating, Reward) };

            foreach (var owner in new[] { first, second })
            {
                using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
                {
                    TraitLevelingHelper.OnIssueSolvedThroughQuest(owner, calculating);
                    TraitLevelingHelper.OnIssueSolvedThroughQuest(owner, DefaultTraits.Honor, Reward);
                    TraitLevelingHelper.OnIssueSolvedThroughAlternativeSolution(owner, calculating);
                    TraitLevelingHelper.OnIssueSolvedThroughBetrayal(owner, calculating);
                }
            }

            Assert.Same(store, Campaign.Current.PlayerTraitDeveloper);
            Assert.Equal(0, store.GetPropertyValue(DefaultTraits.Calculating));
            Assert.Equal(0, store.GetPropertyValue(DefaultTraits.Honor));
            Assert.Equal(1, first.GetTraitLevel(DefaultTraits.Calculating));
            Assert.Equal(0, second.GetTraitLevel(DefaultTraits.Calculating));
        });
    }

    [Fact]
    public void QuestRewards_AreSkipped()
    {
        Assert.False(QuestTraitXpPatches.OnIssueSolvedThroughQuestPrefix());
        Assert.False(QuestTraitXpPatches.OnIssueSolvedThroughQuestSingleTraitPrefix());
        Assert.False(QuestTraitXpPatches.OnIssueSolvedThroughAlternativeSolutionPrefix());
        Assert.False(QuestTraitXpPatches.OnIssueSolvedThroughBetrayalPrefix());
    }
}
