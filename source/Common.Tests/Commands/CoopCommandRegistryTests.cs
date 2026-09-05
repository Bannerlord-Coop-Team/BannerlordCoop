using Common.Commands;
using Moq;
using Serilog;

namespace Common.Tests.Commands;

public class CoopCommandRegistryTests
{
    [Fact]
    public void Constructor_ExposesCommandMetadataAndBuildsUsage()
    {
        var command = new TestCommand(
            "coop.debug.test",
            "capture",
            "Captures one value.",
            expectedArgs: new IExpectedArgs[]
            {
                new ExpectedArgs("value", "The value to capture."),
            });
        var registry = CreateRegistry(command);

        CoopCommandDescriptor descriptor = Assert.Single(registry.Commands);
        Assert.Equal("coop.debug.test.capture", descriptor.FullName);
        Assert.Equal(
            ExpectedUsage(
                "Usage: coop.debug.test.capture <value>",
                string.Empty,
                "Parameters:",
                "- value (required): The value to capture.",
                string.Empty,
                "Note: Wrap parameter values containing spaces in double quotes."),
            descriptor.Usage);
        Assert.Equal(command.Description, descriptor.Description);

        IExpectedArgs expectedArg = Assert.Single(descriptor.ExpectedArgs);
        Assert.Equal("value", expectedArg.Name);
        Assert.Equal("The value to capture.", expectedArg.Description);
        Assert.True(expectedArg.IsRequired);
    }

    [Fact]
    public void Constructor_WhenDescriptionIsMissing_Throws()
    {
        var command = new TestCommand(
            "coop.debug.test",
            "capture",
            string.Empty);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => CreateRegistry(command));

        Assert.Contains("description", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_WhenExpectedArgumentNameIsDuplicated_Throws()
    {
        var command = new TestCommand(
            "coop.debug.test",
            "capture",
            "Captures values.",
            expectedArgs: new IExpectedArgs[]
            {
                new ExpectedArgs("value", "The first value."),
                new ExpectedArgs("value", "The second value."),
            });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => CreateRegistry(command));

        Assert.Contains("defined more than once", exception.Message);
    }

    [Fact]
    public void Constructor_WhenRequiredArgumentFollowsOptionalArgument_Throws()
    {
        var command = new TestCommand(
            "coop.debug.test",
            "capture",
            "Captures values.",
            expectedArgs: new IExpectedArgs[]
            {
                new ExpectedArgs("optional", "An optional value.", isRequired: false),
                new ExpectedArgs("required", "A required value."),
            });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => CreateRegistry(command));

        Assert.Contains("cannot follow optional", exception.Message);
    }

    [Fact]
    public void Constructor_WhenFullNameIsDuplicated_Throws()
    {
        var first = new TestCommand(
            "coop.debug.test",
            "capture",
            "First command.");
        var second = new TestCommand(
            "coop.debug.test",
            "capture",
            "Second command.");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => CreateRegistry(first, second));

        Assert.Contains("coop.debug.test.capture", exception.Message);
    }

    [Fact]
    public void ProcessCommand_TrimsArgumentsBeforePassingThemToCommand()
    {
        var command = new TestCommand(
            "coop.debug.test",
            "capture",
            "Captures one value.",
            expectedArgs: new IExpectedArgs[]
            {
                new ExpectedArgs("value", "The value to capture."),
            });
        var registry = CreateRegistry(command);
        var args = new CoopCommandArgsFactory().FromValues(new[] { "  argument with spaces  " });

        CoopCommandResult result = registry.ProcessCommand("coop.debug.test.capture", args);

        Assert.True(result.Succeeded);
        Assert.Equal("argument with spaces", result.Output);
    }

    [Fact]
    public void ProcessCommand_WhenArgumentsDoNotMatchDefinitions_ReturnsUsageWithoutInvokingCommand()
    {
        var command = new TestCommand(
            "coop.debug.test",
            "capture",
            "Captures one value.",
            expectedArgs: new IExpectedArgs[]
            {
                new ExpectedArgs("value", "The value to capture."),
            });
        var registry = CreateRegistry(command);
        var argsFactory = new CoopCommandArgsFactory();
        ICoopCommandArgs[] invalidArguments =
        {
            argsFactory.FromValues(Array.Empty<string>()),
            argsFactory.FromValues(new[] { string.Empty }),
            argsFactory.FromValues(new[] { "   " }),
            argsFactory.FromValues(new[] { "first", "second" }),
        };

        foreach (ICoopCommandArgs args in invalidArguments)
        {
            CoopCommandResult result = registry.ProcessCommand("coop.debug.test.capture", args);

            Assert.False(result.Succeeded);
            Assert.Equal("invalid_arguments", result.ErrorCode);
            Assert.Equal(
                ExpectedUsage(
                    "Usage: coop.debug.test.capture <value>",
                    string.Empty,
                    "Parameters:",
                    "- value (required): The value to capture.",
                    string.Empty,
                    "Note: Wrap parameter values containing spaces in double quotes."),
                result.Output);
        }

        Assert.Equal(0, command.ProcessCount);
    }

    [Fact]
    public void ProcessCommand_WhenOptionalArgumentIsMissing_InvokesCommand()
    {
        var command = new TestCommand(
            "coop.debug.test",
            "capture",
            "Captures values.",
            expectedArgs: new IExpectedArgs[]
            {
                new ExpectedArgs("value", "The value to capture."),
                new ExpectedArgs("format", "The optional output format.", isRequired: false),
            });
        var registry = CreateRegistry(command);
        var args = new CoopCommandArgsFactory().FromValues(new[] { "first" });

        CoopCommandResult result = registry.ProcessCommand("coop.debug.test.capture", args);

        Assert.True(result.Succeeded);
        Assert.Equal(1, command.ProcessCount);
        Assert.Equal(
            ExpectedUsage(
                "Usage: coop.debug.test.capture <value> [<format>]",
                string.Empty,
                "Parameters:",
                "- value (required): The value to capture.",
                "- format (optional): The optional output format.",
                string.Empty,
                "Note: Wrap parameter values containing spaces in double quotes."),
            Assert.Single(registry.Commands).Usage);
    }

    [Fact]
    public void ProcessCommand_WhenCommandThrows_ReturnsFailure()
    {
        var command = new TestCommand(
            "coop.debug.test",
            "capture",
            "Captures one value.",
            throwOnProcess: true);
        var registry = CreateRegistry(command);
        var args = new CoopCommandArgsFactory().FromValues(Array.Empty<string>());

        CoopCommandResult result = registry.ProcessCommand("coop.debug.test.capture", args);

        Assert.False(result.Succeeded);
        Assert.Equal("command_failed", result.ErrorCode);
        Assert.Contains("test failure", result.Output);
    }

    [Fact]
    public void ProcessCommand_WhenLegacyAliasIsUsed_RoutesToCanonicalCommand()
    {
        var command = new TestCommand(
            "coop.debug.test",
            "capture",
            "Captures one value.",
            expectedArgs: new IExpectedArgs[]
            {
                new ExpectedArgs("value", "The value to capture."),
            });
        var registry = CreateRegistry(
            new Dictionary<string, string>
            {
                { "coop.debug.oldtest.capture", "coop.debug.test.capture" },
            },
            command);
        var args = new CoopCommandArgsFactory().FromValues(new[] { "first" });

        Assert.True(registry.Contains("coop.debug.oldtest.capture"));
        CoopCommandResult result = registry.ProcessCommand("coop.debug.oldtest.capture", args);

        Assert.True(result.Succeeded);
        Assert.Equal("first", result.Output);
        Assert.Equal(1, command.ProcessCount);
        Assert.Equal("coop.debug.test.capture", Assert.Single(registry.LegacyAliases.Values));
    }

    [Fact]
    public void ProcessCommand_WhenLegacyAliasArgumentsAreInvalid_ReturnsCanonicalUsage()
    {
        var command = new TestCommand(
            "coop.debug.test",
            "capture",
            "Captures one value.",
            expectedArgs: new IExpectedArgs[]
            {
                new ExpectedArgs("value", "The value to capture."),
            });
        var registry = CreateRegistry(
            new Dictionary<string, string>
            {
                { "coop.debug.oldtest.capture", "coop.debug.test.capture" },
            },
            command);
        var args = new CoopCommandArgsFactory().FromValues(Array.Empty<string>());

        CoopCommandResult result = registry.ProcessCommand("coop.debug.oldtest.capture", args);

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.StartsWith("Usage: coop.debug.test.capture <value>", result.Output);
        Assert.Equal(0, command.ProcessCount);
    }

    [Fact]
    public void Constructor_WhenLegacyAliasTargetIsMissing_Throws()
    {
        var command = new TestCommand("coop.debug.test", "capture", "Captures values.");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => CreateRegistry(
                new Dictionary<string, string>
                {
                    { "coop.debug.oldtest.capture", "coop.debug.test.missing" },
                },
                command));

        Assert.Contains("coop.debug.oldtest.capture", exception.Message);
    }

    [Fact]
    public void Constructor_WhenLegacyAliasCollidesWithCommand_Throws()
    {
        var command = new TestCommand("coop.debug.test", "capture", "Captures values.");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => CreateRegistry(
                new Dictionary<string, string>
                {
                    { "coop.debug.test.capture", "coop.debug.test.capture" },
                },
                command));

        Assert.Contains("coop.debug.test.capture", exception.Message);
    }

    private static string ExpectedUsage(params string[] lines)
    {
        return string.Join(Environment.NewLine, lines);
    }

    private static CoopCommandRegistry CreateRegistry(params ICoopCommand[] commands)
    {
        return new CoopCommandRegistry(commands, Mock.Of<ILogger>());
    }

    private static CoopCommandRegistry CreateRegistry(
        IReadOnlyDictionary<string, string> legacyAliases,
        params ICoopCommand[] commands)
    {
        return new CoopCommandRegistry(commands, Mock.Of<ILogger>(), legacyAliases);
    }

    private sealed class TestCommand : ICoopCommand
    {
        private readonly bool throwOnProcess;

        public TestCommand(
            string prefix,
            string name,
            string description,
            bool throwOnProcess = false,
            params IExpectedArgs[] expectedArgs)
        {
            Prefix = prefix;
            Name = name;
            Description = description;
            ExpectedArgs = expectedArgs;
            this.throwOnProcess = throwOnProcess;
        }

        public string Prefix { get; }

        public string Name { get; }

        public string Description { get; }

        public IExpectedArgs[] ExpectedArgs { get; }

        public int ProcessCount { get; private set; }

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            ProcessCount++;
            if (throwOnProcess) throw new InvalidOperationException("test failure");

            return new CoopCommandResult(true, args.Count == 0 ? string.Empty : args[0]);
        }
    }
}
