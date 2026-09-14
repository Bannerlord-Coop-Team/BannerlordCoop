using Common.Commands;
using Moq;
using Serilog;

namespace Common.Tests.Commands;

[CollectionDefinition("ModInformation", DisableParallelization = true)]
public class ModInformationCollection
{
}

[Collection("ModInformation")]
public class CoopCommandRegistryTests : IDisposable
{
    private readonly bool originalIsServer = ModInformation.IsServer;

    public void Dispose()
    {
        ModInformation.IsServer = originalIsServer;
    }

    [Theory]
    [InlineData(CoopCommandSide.Both, false, true, "")]
    [InlineData(CoopCommandSide.Both, true, true, "")]
    [InlineData(CoopCommandSide.Server, false, false, "This command is only available on the server")]
    [InlineData(CoopCommandSide.Server, true, true, "")]
    [InlineData(CoopCommandSide.Client, false, true, "")]
    [InlineData(CoopCommandSide.Client, true, false, "This command is only available on the client")]
    public void ProcessCommand_EnforcesCommandSide(
        CoopCommandSide side, bool isServer, bool shouldSucceed, string expectedOutput)
    {
        ModInformation.IsServer = isServer;
        var command = new TestCommand("coop.debug.test", "capture", "Captures values.", side: side);
        var registry = CreateRegistry(command);
        var args = new CoopCommandArgsFactory().FromValues(Array.Empty<string>());

        CoopCommandResult result = registry.ProcessCommand("coop.debug.test.capture", args);

        Assert.Equal(shouldSucceed, result.Succeeded);
        Assert.Equal(expectedOutput, result.Output);
        Assert.Equal(shouldSucceed ? null : "command_wrong_side", result.ErrorCode);
        Assert.Equal(shouldSucceed ? 1 : 0, command.ProcessCount);
    }

    [Theory]
    [InlineData(CoopCommandSide.Server, false, "This command is only available on the server")]
    [InlineData(CoopCommandSide.Client, true, "This command is only available on the client")]
    public void ProcessCommand_WhenArgumentsAreInvalidOnWrongSide_ReturnsSideRejection(
        CoopCommandSide side, bool isServer, string expectedOutput)
    {
        ModInformation.IsServer = isServer;
        var command = new TestCommand(
            "coop.debug.test", "capture", "Captures one value.", side: side,
            expectedArgs: new IExpectedArgs[] { new ExpectedArgs("value", "The value to capture.") });
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
            Assert.Equal("command_wrong_side", result.ErrorCode);
            Assert.Equal(expectedOutput, result.Output);
        }

        Assert.Equal(0, command.ProcessCount);
    }

    [Theory]
    [InlineData(CoopCommandSide.Server, true)]
    [InlineData(CoopCommandSide.Client, false)]
    public void ProcessCommand_WhenRuntimeRoleChanges_UsesCurrentRole(CoopCommandSide side, bool allowedIsServer)
    {
        ModInformation.IsServer = allowedIsServer;
        var command = new TestCommand("coop.debug.test", "capture", "Captures values.", side: side);
        var registry = CreateRegistry(command);
        var args = new CoopCommandArgsFactory().FromValues(Array.Empty<string>());

        Assert.True(registry.ProcessCommand("coop.debug.test.capture", args).Succeeded);
        ModInformation.IsServer = !allowedIsServer;
        Assert.Equal("command_wrong_side", registry.ProcessCommand("coop.debug.test.capture", args).ErrorCode);
        Assert.Equal(1, command.ProcessCount);
        ModInformation.IsServer = allowedIsServer;
        Assert.True(registry.ProcessCommand("coop.debug.test.capture", args).Succeeded);
        Assert.Equal(2, command.ProcessCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Constructor_WhenCommandSideIsUndefined_Throws(int side)
    {
        var command = new TestCommand(
            "coop.debug.test", "capture", "Captures values.", side: (CoopCommandSide)side);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => CreateRegistry(command));

        Assert.Contains("coop.debug.test.capture", exception.Message);
        Assert.Contains("invalid command side", exception.Message);
    }

    [Theory]
    [InlineData(CoopCommandSide.Both)]
    [InlineData(CoopCommandSide.Server)]
    [InlineData(CoopCommandSide.Client)]
    public void Constructor_SnapshotsCommandSideMetadata(CoopCommandSide side)
    {
        var command = new TestCommand("coop.debug.test", "capture", "Captures values.", side: side);
        var registry = CreateRegistry(command);
        CoopCommandDescriptor descriptor = Assert.Single(registry.Commands);
        command.Side = side == CoopCommandSide.Server ? CoopCommandSide.Client : CoopCommandSide.Server;
        ModInformation.IsServer = side != CoopCommandSide.Client;

        Assert.Equal(side, descriptor.Side);
        Assert.True(registry.ProcessCommand(
            descriptor.FullName, new CoopCommandArgsFactory().FromValues(Array.Empty<string>())).Succeeded);
    }

    [Fact]
    public void DescriptorConstructor_WithoutSide_DefaultsToBoth()
    {
        var descriptor = new CoopCommandDescriptor("coop.debug.test", "capture", "Captures values.", Array.Empty<IExpectedArgs>());

        Assert.Equal(CoopCommandSide.Both, descriptor.Side);
        Assert.Equal(0, (int)CoopCommandSide.Both);
    }

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

    private static string ExpectedUsage(params string[] lines)
    {
        return string.Join(Environment.NewLine, lines);
    }

    private static CoopCommandRegistry CreateRegistry(params ICoopCommand[] commands)
    {
        return new CoopCommandRegistry(commands, Mock.Of<ILogger>());
    }

    private sealed class TestCommand : ICoopCommand
    {
        private readonly bool throwOnProcess;

        public TestCommand(
            string prefix,
            string name,
            string description,
            bool throwOnProcess = false,
            CoopCommandSide side = CoopCommandSide.Both,
            params IExpectedArgs[] expectedArgs)
        {
            Prefix = prefix;
            Name = name;
            Description = description;
            Side = side;
            ExpectedArgs = expectedArgs;
            this.throwOnProcess = throwOnProcess;
        }

        public string Prefix { get; }

        public string Name { get; }

        public string Description { get; }

        public CoopCommandSide Side { get; set; }

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
