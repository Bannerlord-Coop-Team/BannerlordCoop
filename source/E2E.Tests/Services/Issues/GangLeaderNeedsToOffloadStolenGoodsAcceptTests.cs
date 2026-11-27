using Common.Logging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest;

namespace E2E.Tests.Services.Issues;

public class GangLeaderNeedsToOffloadStolenGoodsAcceptTests : IDisposable
{
    // The quest is built without its constructor, so only the hideout reveal of the accept runs its vanilla body
    private static readonly MethodBase[] AcceptSteps =
    {
        AccessTools.Method(typeof(QuestBase), nameof(QuestBase.StartQuest)),
        AccessTools.Method(typeof(QuestBase), nameof(QuestBase.AddLog)),
        AccessTools.Method(typeof(QuestBase), nameof(QuestBase.AddTrackedObject)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.PlayerStartsQuestLogText)),
    };

    private readonly string hideoutId;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance ClientA => TestEnvironment.Clients.First();
    private EnvironmentInstance ClientB => TestEnvironment.Clients.Last();

    public GangLeaderNeedsToOffloadStolenGoodsAcceptTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        hideoutId = TestEnvironment.CreateRegisteredObject<Settlement>();

        foreach (var instance in new[] { Server, ClientA, ClientB })
        {
            instance.Call(() => instance.GetRegisteredObject<Settlement>(hideoutId)._isVisible = false);
        }
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void AcceptRevealsTheHideoutOnlyForTheOwnerWithoutManagedWriteError()
    {
        int errorCount = 0;
        Action<string> captureError = message =>
        {
            if (message.Contains("Client updated managed") && message.Contains("Settlement.IsVisible"))
                Interlocked.Increment(ref errorCount);
        };
        OutputSinkManager.AddLogCallback(captureError);

        try
        {
            ClientA.Call(() =>
            {
                var quest = ObjectHelper.SkipConstructor<Quest>();
                AccessTools.Field(typeof(Quest), nameof(Quest._questHideout)).SetValue(quest, ClientA.GetRegisteredObject<Settlement>(hideoutId));

                quest.QuestAcceptedConsequences();
            }, AcceptSteps);

            Assert.Equal(0, Volatile.Read(ref errorCount));
            Assert.Single(ClientA.InternalMessages.GetMessages<QuestAcceptanceStarted>());
            ClientA.Call(() => Assert.True(ClientA.GetRegisteredObject<Settlement>(hideoutId).IsVisible));
            Server.Call(() => Assert.False(Server.GetRegisteredObject<Settlement>(hideoutId).IsVisible));
            ClientB.Call(() => Assert.False(ClientB.GetRegisteredObject<Settlement>(hideoutId).IsVisible));

            // Any other client write of the hideout still logs
            ClientA.Call(() => ClientA.GetRegisteredObject<Settlement>(hideoutId).IsVisible = false);
            Assert.Equal(1, Volatile.Read(ref errorCount));
        }
        finally
        {
            OutputSinkManager.RemoveLogCallback(captureError);
        }
    }
}
