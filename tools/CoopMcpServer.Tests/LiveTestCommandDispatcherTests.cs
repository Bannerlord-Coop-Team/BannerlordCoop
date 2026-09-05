using Common.Commands;
using GameInterface.Services.LiveTesting;
using Serilog;

namespace CoopMcpServer.Tests
{
    public sealed class LiveTestCommandDispatcherTests
    {
        [Theory]
        [InlineData("coop")]
        [InlineData("coop.connection")]
        [InlineData("coop.debug.test")]
        public void EveryRegisteredFrameworkPrefixIsListedAndExecutable(string prefix)
        {
            var command = new CaptureCommand(prefix);
            using var logger = new LoggerConfiguration().CreateLogger();
            var registry = new CoopCommandRegistry(new[] { command }, logger);
            var dispatcher = new LiveTestCommandDispatcher(registry, new CoopCommandArgsFactory());
            string name = prefix + ".capture";
            Assert.Contains(name, dispatcher.GetCommandNames());
            var result = dispatcher.Execute(name, new List<string> { "argument with spaces", "quoted \"value\"" });
            Assert.True(result.Found);
            Assert.Equal("argument with spaces|quoted \"value\"", result.Output);
        }

        [Theory]
        [InlineData("campaign.do_something")]
        [InlineData("coop.unregistered")]
        public void UnregisteredNonDebugCommandsNeverReachVanilla(string name)
        {
            var dispatcher = new LiveTestCommandDispatcher();
            Assert.DoesNotContain(name, dispatcher.GetCommandNames());
            Assert.False(dispatcher.Execute(name, new List<string>()).Found);
        }

        [Fact]
        public void LegacyDebugCommandsRemainAvailable()
        {
            var dispatcher = new LiveTestCommandDispatcher();
            Assert.Contains("coop.debug.legacy", dispatcher.GetCommandNames());
            Assert.True(dispatcher.Execute("coop.debug.legacy", new List<string>()).Found);
        }

        private sealed class CaptureCommand(string prefix) : ICoopCommand
        {
            public string Prefix => prefix;
            public string Name => "capture";
            public string Description => "Capture arguments.";
            public IExpectedArgs[] ExpectedArgs => new IExpectedArgs[] { new ExpectedArgs("first", "First value."), new ExpectedArgs("second", "Second value.") };
            public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => new(true, string.Join("|", args));
        }
    }
}

// These fakes exercise the linked dispatcher without loading Bannerlord or native DLLs.
namespace Common
{
    public static class GameThread
    {
        public static void Run(Action action, bool blocking) => action();
    }
}

namespace TaleWorlds.Library
{
    public static class CommandLineFunctionality
    {
        private static readonly Dictionary<string, object> AllFunctions = new()
        {
            ["coop.debug.legacy"] = new(), ["campaign.do_something"] = new(), ["coop.unregistered"] = new(),
        };
        public static void CollectCommandLineFunctions() { }
        public static string CallFunction(string command, List<string> arguments, out bool found)
        {
            if (!command.StartsWith("coop.debug.", StringComparison.Ordinal))
                throw new InvalidOperationException("Arbitrary vanilla dispatch is forbidden in this test.");
            found = AllFunctions.ContainsKey(command);
            return "legacy result";
        }
    }
}
