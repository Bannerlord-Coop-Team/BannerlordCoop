using E2E.Tests.Environment;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>Checks absolute deadline application on existing acceptance replicas.</summary>
public class QuestAcceptanceDeadlineTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;

    public QuestAcceptanceDeadlineTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(environment);
    }

    public void Dispose() => environment.Dispose();

    [Theory]
    [InlineData(true, true, 0L)]
    [InlineData(true, true, 78719763460921L)]
    [InlineData(true, true, long.MaxValue)]
    [InlineData(false, true, 0L)]
    [InlineData(false, true, 78719763460921L)]
    [InlineData(false, true, long.MaxValue)]
    [InlineData(true, false, 0L)]
    [InlineData(false, false, 0L)]
    [InlineData(true, false, 78719763460921L)]
    [InlineData(false, false, 78719763460921L)]
    public void ReceivedDeadlineWaitsForTheGameThreadAndReplaysIdempotentlyWithLegacyFallback(
        bool isOwner, bool hasDeadline, long deadline)
    {
        var client = isOwner ? scenario.ClientA : scenario.ClientB;
        var quest = scenario.AttachQuest(client);
        var originalDue = quest.QuestDueTime.NumTicks;
        var accepted = new NetworkQuestSolutionAccepted(scenario.NotableId, QuestScenario.IssueId,
            QuestScenario.ControllerA, 1f, Array.Empty<byte>(), deadline, hasDeadline);

        client.SimulateMessage(scenario.Server.NetPeer, accepted, markGameThread: false);
        Assert.Equal(1, client.PendingGameThreadActionCount);
        Assert.Equal(originalDue, quest.QuestDueTime.NumTicks);
        client.PumpGameThread();
        Assert.Equal(hasDeadline ? deadline : originalDue, quest.QuestDueTime.NumTicks);

        client.SimulateMessage(scenario.Server.NetPeer, accepted, markGameThread: false);
        client.PumpGameThread();
        Assert.Equal(hasDeadline ? deadline : originalDue, quest.QuestDueTime.NumTicks);
        Assert.Same(quest, scenario.QuestOf(client));
        Assert.Empty(quest.JournalEntries);
        Assert.Empty(client.NetworkSentMessages.GetMessages<RequestQuestSolutionAccept>());
        Assert.Equal(0, client.PendingGameThreadActionCount);
    }
}
