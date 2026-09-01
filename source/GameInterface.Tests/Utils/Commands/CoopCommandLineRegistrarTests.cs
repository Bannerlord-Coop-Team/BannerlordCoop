using Common.Commands;
using GameInterface.Utils.Commands;
using Moq;
using Serilog;
using System;
using System.Runtime.CompilerServices;
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

    [Fact]
    public void Registration_WhenThreeGenerationsOverlap_UnlinksDisposedPredecessors()
    {
        var argsFactory = new CoopCommandArgsFactory();
        var registrations = CreateOverlappingRegistrations(argsFactory);

        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.False(registrations.FirstRegistrar.IsAlive);
            Assert.False(registrations.FirstRegistry.IsAlive);
            Assert.False(registrations.SecondRegistrar.IsAlive);
            Assert.False(registrations.SecondRegistry.IsAlive);

            string output = CommandLineFunctionality.CallFunction(
                FullName,
                "current",
                out bool found);

            Assert.True(found);
            Assert.Equal("current", output);
        }
        finally
        {
            registrations.ActiveRegistrar.Dispose();
        }

        Assert.False(CommandLineFunctionality.HasFunctionForCommand(FullName));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (
        WeakReference FirstRegistrar,
        WeakReference FirstRegistry,
        WeakReference SecondRegistrar,
        WeakReference SecondRegistry,
        CoopCommandLineRegistrar ActiveRegistrar) CreateOverlappingRegistrations(
            CoopCommandArgsFactory argsFactory)
    {
        var firstRegistry = new CoopCommandRegistry(
            new[] { new CaptureCommand() },
            Mock.Of<ILogger>());
        var secondRegistry = new CoopCommandRegistry(
            new[] { new CaptureCommand() },
            Mock.Of<ILogger>());
        var thirdRegistry = new CoopCommandRegistry(
            new[] { new CaptureCommand() },
            Mock.Of<ILogger>());
        var firstRegistrar = new CoopCommandLineRegistrar(firstRegistry, argsFactory);
        var secondRegistrar = new CoopCommandLineRegistrar(secondRegistry, argsFactory);
        var thirdRegistrar = new CoopCommandLineRegistrar(thirdRegistry, argsFactory);

        var firstRegistrarReference = new WeakReference(firstRegistrar);
        var firstRegistryReference = new WeakReference(firstRegistry);
        var secondRegistrarReference = new WeakReference(secondRegistrar);
        var secondRegistryReference = new WeakReference(secondRegistry);

        firstRegistrar.Dispose();
        secondRegistrar.Dispose();

        return (
            firstRegistrarReference,
            firstRegistryReference,
            secondRegistrarReference,
            secondRegistryReference,
            thirdRegistrar);
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
