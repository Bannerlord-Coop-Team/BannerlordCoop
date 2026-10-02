using E2E.Tests.Environment;
using GameInterface.Services.Issues.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class OwnerTraitXpProgressTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    public OwnerTraitXpProgressTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    [Fact]
    public void RewardsAndPenaltiesAccumulateForEachPlayerWithoutChangingTheCampaignTraitDeveloper()
    {
        var firstId = environment.CreateRegisteredObject<Hero>();
        var secondId = environment.CreateRegisteredObject<Hero>();
        var server = environment.Server;
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Hero>(firstId, out var first));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(secondId, out var second));
            Campaign.Current.PlayerTraitDeveloper ??= new PropertyOwner<PropertyObject>();
            var campaignXp = Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor);
            server.Resolve<IOwnerTraitXpProgress>().Apply(first, DefaultTraits.Honor, 100);
            server.Resolve<IOwnerTraitXpProgress>().Apply(second, DefaultTraits.Honor, -10);
            server.Resolve<IOwnerTraitXpProgress>().Apply(first, DefaultTraits.Honor, -10);

            var registry = server.Resolve<PendingRegistry<PropertyOwner<PropertyObject>>>();
            Assert.True(registry.TryGet(first, out var firstProgress));
            Assert.True(registry.TryGet(second, out var secondProgress));
            Assert.Equal(90, firstProgress.GetPropertyValue(DefaultTraits.Honor));
            Assert.Equal(-10, secondProgress.GetPropertyValue(DefaultTraits.Honor));
            Assert.Equal(campaignXp, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
        });

        foreach (var client in environment.Clients)
        {
            client.Call(() => Assert.Empty(client.Resolve<PendingRegistry<PropertyOwner<PropertyObject>>>().Snapshot()));
        }
    }
}
