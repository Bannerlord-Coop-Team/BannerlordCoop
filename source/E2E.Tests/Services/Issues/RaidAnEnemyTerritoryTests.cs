using E2E.Tests.Environment;
using GameInterface.Services.Issues.Framework.Interface;
using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class RaidAnEnemyTerritoryTests : IDisposable
{
    private readonly E2ETestEnvironment testEnvironment;

    public RaidAnEnemyTerritoryTests(ITestOutputHelper output)
    {
        testEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose() => testEnvironment.Dispose();

    [Fact]
    public void RaidQuest_IsRegisteredOnTheNewFrameworkForEveryPeer()
    {
        foreach (var instance in new[] { testEnvironment.Server }.Concat(testEnvironment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.Resolve<IQuestTypeRegistry>().TryGetByName(
                    "RaidAnEnemyTerritoryIssue", out var descriptor));
                Assert.NotNull(descriptor.CreationCaptureStrategy);
                Assert.NotNull(descriptor.QuestSolutionAcceptStrategy);
                Assert.NotNull(descriptor.FinalizationProofStrategy);
            });
        }
    }
}
