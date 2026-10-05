#if DEBUG
using Common.Commands;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Commands;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class QuestIsolationFixtureCommandsTests : IDisposable
{
    private const string StructuredPrefix = "LIVE_TEST_JSON=";

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();

    public QuestIsolationFixtureCommandsTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void State_HeroWithoutIssue_ReportsNoIssueQuestOrOwnerOnEverySide()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();

        foreach (var (instance, side) in new[] { (Server, "server"), (Client, "client") })
        {
            instance.Call(() =>
            {
                var result = State(instance).ProcessCommand(Args(heroId));

                Assert.True(result.Succeeded, result.Output);
                Assert.StartsWith(StructuredPrefix, result.Output);
                var state = JObject.Parse(result.Output.Substring(StructuredPrefix.Length));
                Assert.Equal(side, (string)state["side"]);
                Assert.Equal(heroId, (string)state["heroId"]);
                Assert.Equal(JTokenType.Null, state["issue"].Type);
                Assert.Equal(JTokenType.Null, state["quest"].Type);
                Assert.Equal(JTokenType.Null, state["owner"]["controllerId"].Type);
                Assert.False((bool)state["owner"]["isLocalPeerOwner"]);
            });
        }
    }

    [Fact]
    public void Accept_WithoutServerAllowedConversation_DoesNotRequestTheQuest()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();

        Client.Call(() =>
        {
            var command = new QuestIsolationFixtureCommands.AcceptCoopCommand(
                Client.Resolve<IObjectManager>(),
                Client.Resolve<IIssueConversationTracker>(),
                Client.Resolve<IControllerIdProvider>());

            var result = command.ProcessCommand(Args(heroId));

            Assert.False(result.Succeeded);
            Assert.Contains("has not allowed a tracked conversation", result.Output);
        });

        Assert.Empty(Client.InternalMessages.GetMessages<QuestTypeQuestSolutionAcceptTriggered>());
    }

    [Fact]
    public void OpenConversation_OnServer_IsRejectedWithoutPublishing()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            var command = new QuestIsolationFixtureCommands.OpenConversationCoopCommand(
                Server.Resolve<IObjectManager>(),
                Server.Resolve<IControllerIdProvider>());

            var result = command.ProcessCommand(Args(heroId));

            Assert.False(result.Succeeded);
            Assert.Contains("is client-only", result.Output);
        });

        Assert.Empty(Server.InternalMessages.GetMessages<IssueConversationOpenedLocally>());
    }

    private static QuestIsolationFixtureCommands.StateCoopCommand State(EnvironmentInstance instance) =>
        new QuestIsolationFixtureCommands.StateCoopCommand(
            instance.Resolve<IObjectManager>(),
            instance.Resolve<IIssueOwnershipRegistry>(),
            instance.Resolve<IIssueGenerationRegistry>(),
            instance.Resolve<IIssueConversationTracker>(),
            instance.Resolve<IControllerIdProvider>());

    private static ICoopCommandArgs Args(params string[] values) => new CoopCommandArgsFactory().FromValues(values);
}
#endif
