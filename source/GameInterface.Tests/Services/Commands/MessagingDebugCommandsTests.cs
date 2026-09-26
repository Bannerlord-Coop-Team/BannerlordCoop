#if DEBUG
using Common.Commands;
using Common.Messaging;
using GameInterface.Services.GameDebug.Commands;
using System;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace GameInterface.Tests.Services.Commands;

// The command reads the process-wide log sink, so these tests stay in one class (xUnit runs them one at a
// time) and the command itself only counts lines carrying its own probe message.
public class MessagingDebugCommandsTests
{
    [Fact]
    public void PublishThrowing_WhenTheProbeSubscriberThrows_ReturnsTheFailureLineNamingHandlerAndMessage()
    {
        var command = new MessagingDebugCommands.MessagingPublishThrowingCoopCommand(new MessageBroker());

        CoopCommandResult result = command.ProcessCommand(NoArgs());

        Assert.True(result.Succeeded, result.Output);
        Assert.Contains("Failed to run \"Handle\" for \"MessagingProbeMessage\"", result.Output);
        Assert.Contains("messaging probe failure", result.Output);
        Assert.DoesNotContain(nameof(TargetInvocationException), result.Output);
    }

    [Fact]
    public void PublishThrowing_WhenRunTwiceOnTheSameBroker_ReportsOneFailurePerRun()
    {
        var broker = new MessageBroker();
        var command = new MessagingDebugCommands.MessagingPublishThrowingCoopCommand(broker);

        command.ProcessCommand(NoArgs());
        CoopCommandResult second = command.ProcessCommand(NoArgs());

        Assert.True(second.Succeeded, second.Output);
        Assert.Equal(1, Regex.Matches(second.Output, "Failed to run").Count);
    }

    [Fact]
    public void PublishThrowing_WithNoBroker_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MessagingDebugCommands.MessagingPublishThrowingCoopCommand(null));
    }

    private static ICoopCommandArgs NoArgs()
    {
        return new CoopCommandArgsFactory().FromValues(Array.Empty<string>());
    }
}
#endif
