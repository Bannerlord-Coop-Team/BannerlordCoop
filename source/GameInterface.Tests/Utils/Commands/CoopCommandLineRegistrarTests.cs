using Common.Commands;
using GameInterface.Utils.Commands;
using Moq;
using Serilog;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Utils.Commands;

public class CoopCommandLineRegistrarTests
{
    private const string FullName = "coop.debug.command_framework_test.capture";

    [Fact]
    public void Registration_ExposesCommandToBannerlordAndParsesQuotedArguments()
    {
        var registry = new CoopCommandRegistry(
            new[] { new CaptureCommand() },
            Mock.Of<ILogger>());
        var argsFactory = new CoopCommandArgsFactory();

        using (var registrar = new CoopCommandLineRegistrar(registry, argsFactory))
        {
            string output = CommandLineFunctionality.CallFunction(
                FullName,
                "\"argument with spaces\" tail",
                out bool found);

            Assert.True(found);
            Assert.Equal("argument with spaces|tail", output);
            Assert.True(CommandLineFunctionality.HasFunctionForCommand(FullName));
        }

        Assert.False(CommandLineFunctionality.HasFunctionForCommand(FullName));
    }

    [Fact]
    public void Registration_WhenFrameworkRegistrationAlreadyExists_ReplacesItForNewLifetime()
    {
        var registry = new CoopCommandRegistry(
            new[] { new CaptureCommand() },
            Mock.Of<ILogger>());
        var argsFactory = new CoopCommandArgsFactory();
        var first = new CoopCommandLineRegistrar(registry, argsFactory);
        var second = new CoopCommandLineRegistrar(registry, argsFactory);

        try
        {
            first.Dispose();

            string output = CommandLineFunctionality.CallFunction(
                FullName,
                "current",
                out bool found);

            Assert.True(found);
            Assert.Equal("current", output);
        }
        finally
        {
            first.Dispose();
            second.Dispose();
        }

        Assert.False(CommandLineFunctionality.HasFunctionForCommand(FullName));
    }

    private sealed class CaptureCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.command_framework_test";

        public string Name => "capture";

        public string Description => "Captures arguments for the command framework test.";

        public IExpectedArgs[] ExpectedArgs => new IExpectedArgs[]
        {
            new ExpectedArgs("first", "The first value."),
            new ExpectedArgs("second", "The second value.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            return new CoopCommandResult(true, string.Join("|", args));
        }
    }
}
