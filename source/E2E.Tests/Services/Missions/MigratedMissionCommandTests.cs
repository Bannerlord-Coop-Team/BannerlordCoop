using Common.Commands;
using Missions;
using Missions.Agents.Handlers;
using Missions.Battles;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class MigratedMissionCommandTests
{
    private static readonly string[] CommandNamespaces =
    {
        "Missions.Agents.Handlers",
        "Missions.Battles",
    };

    [Fact]
    public void Commands_HaveValidUniqueMetadata()
    {
        ICoopCommand[] commands = CreateCommands();
        var logger = new LoggerConfiguration().CreateLogger();

        var registry = new CoopCommandRegistry(commands, logger);

#if DEBUG
        Assert.Equal(26, commands.Length);
#else
        Assert.Equal(15, commands.Length);
#endif
        Assert.Equal(commands.Length, registry.Commands.Count);
        Assert.All(commands, AssertNormalizedMetadata);
    }

    [Fact]
    public void ConditionalArgumentMetadata_MatchesLegacyBehavior()
    {
#if DEBUG
        var replication = new ReplicationFixtureCommand(new BattleLegacyCommandResult());
        Assert.Equal(new[] { "mode", "connected_controller_id" },
            replication.ExpectedArgs.Select(argument => argument.Name));
        Assert.True(replication.ExpectedArgs[0].IsRequired);
        Assert.False(replication.ExpectedArgs[1].IsRequired);

        var movementResultFactory = new MovementLegacyCommandResult();
        Assert.Single(new ForceRateCommand(movementResultFactory).ExpectedArgs);
        Assert.Single(new ForceReceiverCapCommand(movementResultFactory).ExpectedArgs);
#endif
        var resultFactory = new BattleLegacyCommandResult();
        var mountState = new MountStateCommand(resultFactory);
        Assert.Single(mountState.ExpectedArgs);
        Assert.False(mountState.ExpectedArgs[0].IsRequired);

        var ladderState = new LadderStateCommand(resultFactory);
        Assert.Single(ladderState.ExpectedArgs);
        Assert.False(ladderState.ExpectedArgs[0].IsRequired);
    }

    [Fact]
    public void Registry_RejectsInvalidCountBeforeCallingMissionLogic()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var command = new CaptureMountPoseCommand(new BattleLegacyCommandResult());
        var registry = new CoopCommandRegistry(new[] { command }, logger);
        var argsFactory = new CoopCommandArgsFactory();

        CoopCommandResult result = registry.ProcessCommand(
            "coop.debug.battle.capture_mount_pose",
            argsFactory.FromValues(Array.Empty<string>()));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.Contains("<mount_agent_id>", result.Output);
    }

    [Theory]
    [InlineData("No active coop battle mission", false)]
    [InlineData("Charged 2 locally owned formation(s) with 12 active agent(s)", true)]
    public void LegacyResult_MapsSuccessAndFailure(string output, bool succeeded)
    {
        CoopCommandResult result = new BattleLegacyCommandResult().FromOutput(output);

        Assert.Equal(succeeded, result.Succeeded);
        Assert.Equal(succeeded ? null : "command_failed", result.ErrorCode);
        Assert.Equal(output, result.Output);
    }

    private static ICoopCommand[] CreateCommands()
    {
        return typeof(MissionModule).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(ICoopCommand).IsAssignableFrom(type))
            .Where(type => CommandNamespaces.Contains(type.Namespace))
            .Select(CreateCommand)
            .ToArray();
    }

    private static ICoopCommand CreateCommand(Type type)
    {
        object resultFactory = type.Namespace switch
        {
            "Missions.Agents.Handlers" => new MovementLegacyCommandResult(),
            "Missions.Battles" => new BattleLegacyCommandResult(),
            _ => throw new InvalidOperationException(type.FullName),
        };
        return (ICoopCommand)Activator.CreateInstance(type, resultFactory);
    }

    private static void AssertNormalizedMetadata(ICoopCommand command)
    {
        Assert.StartsWith("coop.", command.Prefix);
        Assert.Matches("^[a-z0-9_]+(?:\\.[a-z0-9_]+)*$", command.Prefix);
        Assert.Matches("^[a-z0-9_]+$", command.Name);
        Assert.False(string.IsNullOrWhiteSpace(command.Description));
        Assert.NotNull(command.ExpectedArgs);

        bool optionalFound = false;
        var argumentNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (IExpectedArgs expectedArg in command.ExpectedArgs)
        {
            Assert.Matches("^[a-z0-9_]+$", expectedArg.Name);
            Assert.True(argumentNames.Add(expectedArg.Name));
            Assert.False(string.IsNullOrWhiteSpace(expectedArg.Description));
            Assert.False(expectedArg.IsRequired && optionalFound);
            optionalFound |= !expectedArg.IsRequired;
        }
    }
}
