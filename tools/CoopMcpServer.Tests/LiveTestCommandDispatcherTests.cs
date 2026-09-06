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
