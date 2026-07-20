using Common;
using GameInterface.Services.LiveTesting;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.LiveTesting;

public class LiveTestCommandDispatcherTests
{
    private const string DebugCommand = "coop.debug.live_testing_dispatcher_test.capture";
    private const string NonDebugCommand = "live_testing_dispatcher_test.capture";

    private static int nonDebugInvocations;

    static LiveTestCommandDispatcherTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void Execute_WhenDebugCommandExists_PreservesArguments()
    {
        var arguments = new List<string>
        {
            "argument with spaces",
            "identifier-42",
        };

        LiveTestCommandResult result = new LiveTestCommandDispatcher().Execute(DebugCommand, arguments);

        Assert.True(result.Found);
        Assert.Equal("argument with spaces|identifier-42", result.Output);
    }

    [Fact]
    public void EnsureReady_CollectsCommandFunctions()
    {
        Assert.True(new LiveTestCommandDispatcher().EnsureReady());
    }

    [Fact]
    public void Execute_WhenDebugCommandDoesNotExist_ReturnsNotFound()
    {
        const string command = "coop.debug.live_testing_dispatcher_test.missing";

        LiveTestCommandResult result = new LiveTestCommandDispatcher().Execute(command, new List<string>());

        Assert.False(result.Found);
        Assert.Equal($"Could not find the command {command}", result.Output);
    }

    [Fact]
    public void Execute_WhenCommandIsNotDebug_RejectsWithoutInvokingIt()
    {
        nonDebugInvocations = 0;

        LiveTestCommandResult result = new LiveTestCommandDispatcher().Execute(NonDebugCommand, new List<string>());

        Assert.False(result.Found);
        Assert.Equal("Only coop.debug. commands may be run through live testing", result.Output);
        Assert.Equal(0, nonDebugInvocations);
    }

    [CommandLineFunctionality.CommandLineArgumentFunction("capture", "coop.debug.live_testing_dispatcher_test")]
    private static string CaptureArguments(List<string> arguments)
    {
        return string.Join("|", arguments);
    }

    [CommandLineFunctionality.CommandLineArgumentFunction("capture", "live_testing_dispatcher_test")]
    private static string CaptureNonDebugInvocation(List<string> arguments)
    {
        nonDebugInvocations++;
        return "invoked";
    }
}
