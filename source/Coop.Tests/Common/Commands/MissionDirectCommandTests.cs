using Common.Commands;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Commands;

[Collection("Mission.Current")]
public class MissionDirectCommandTests
{
    private static readonly HashSet<string> OwningTypes = new HashSet<string>
    {
        "MovementDebugCommands",
        "BattleDebugCommands",
    };

#if DEBUG
    // Required-only commands that still check args.Count, which CoopCommandRegistry.ArgumentsAreValid already enforces.
    private static readonly HashSet<string> RequiredOnlyCountReaders = new HashSet<string>
    {
        "cancel_fixture_mission_ready",
        "defer_fixture_mission_ready",
    };
#endif

    [Fact]
    public void MissionCommands_AreDirectNormalizedCommands()
    {
        Type[] commandTypes = GetCommandTypes();
#if DEBUG
        Assert.Equal(38, commandTypes.Length);
#else
        Assert.Equal(15, commandTypes.Length);
#endif
        ICoopCommand[] commands = commandTypes
            .Select(Instantiate)
            .ToArray();
        var registry = new CoopCommandRegistry(commands, new LoggerConfiguration().CreateLogger());

        Assert.Equal(commands.Length, registry.Commands.Count);
#if DEBUG
        Assert.Contains(commands, command => command.Name == "peer_state");
        Assert.Contains(commands, command => command.Name == "controller_agents");
        Assert.Contains(commands, command => command.Name == "drive_owned_agents");
        Assert.Contains(commands, command => command.Name == "cancel_owned_agent_drive");
        Assert.Contains(commands, command => command.Name == "owned_agent_drive_state");
#endif
        Assert.All(commandTypes, type =>
        {
            Assert.Equal(typeof(object), type.BaseType);
            Assert.Equal(new[] { typeof(ICoopCommand) }, type.GetInterfaces());
            Assert.EndsWith("CoopCommand", type.Name);
        });
        Assert.All(commands, command =>
        {
            Assert.Matches("^coop(?:\\.[a-z0-9_]+)+$", command.Prefix);
            Assert.Matches("^[a-z0-9]+(?:_[a-z0-9]+)*$", command.Name);
            Assert.False(string.IsNullOrWhiteSpace(command.Description));
            Assert.NotNull(command.ExpectedArgs);
        });
    }

    [Fact]
    public void ProcessCommand_ReadsCountOnlyForOptionalArguments()
    {
        ICoopCommand[] countReaders = GetCommandTypes()
            .Where(type => CallsArgumentCount(type.GetMethod(nameof(ICoopCommand.ProcessCommand))))
            .Select(Instantiate)
            .ToArray();
        string[] names = countReaders.Select(command => command.Name).OrderBy(name => name).ToArray();

#if DEBUG
        Assert.All(RequiredOnlyCountReaders, name => Assert.Contains(name, names));
        Assert.All(countReaders, command => Assert.True(
            RequiredOnlyCountReaders.Contains(command.Name) || command.ExpectedArgs.Any(arg => !arg.IsRequired),
            $"{command.Name} reads args.Count without an optional argument"));
#else
        Assert.Equal(new[] { "ladder_state", "mount_state" }, names);
#endif
    }

    [Fact]
    public void MissionRegistry_RejectsInvalidArgumentCount()
    {
        ICoopCommand command = CreateCommand("move_cavalry");
        var registry = new CoopCommandRegistry(
            new[] { command },
            new LoggerConfiguration().CreateLogger());

        CoopCommandResult result = registry.ProcessCommand(
            $"{command.Prefix}.{command.Name}",
            new TestArgs(Array.Empty<string>()));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_arguments", result.ErrorCode);
    }

    [Fact]
    public void FocusLadder_InvalidMachineId_IsExplicitFailure()
    {
        ICoopCommand command = CreateCommand("focus_ladder");

        CoopCommandResult result = command.ProcessCommand(new TestArgs(new[] { "not-an-id" }));

        Assert.False(result.Succeeded);
        Assert.Equal("command_failed", result.ErrorCode);
    }

#if DEBUG
    [Fact]
    public void BattleFixture_MissingMission_IsExplicitFailure()
    {
        Assert.True(Mission.Current == null, "Mission.Current is set, a MissionCurrentScope leaked or ran in parallel");
        ICoopCommand command = CreateCommand("replication_fixture");

        CoopCommandResult result = command.ProcessCommand(new TestArgs(new[] { "initial" }));

        Assert.False(result.Succeeded);
        Assert.Equal("command_failed", result.ErrorCode);
    }
#endif

    private static bool CallsArgumentCount(MethodInfo method)
    {
        byte[] il = method.GetMethodBody().GetILAsByteArray();
        for (int index = 0; index <= il.Length - sizeof(int); index++)
        {
            try
            {
                MethodBase referencedMethod = method.Module.ResolveMethod(BitConverter.ToInt32(il, index));
                if (referencedMethod.Name == "get_Count" &&
                    (referencedMethod.DeclaringType == typeof(ICoopCommandArgs) ||
                     referencedMethod.DeclaringType == typeof(IReadOnlyCollection<string>)))
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
            }
        }

        return false;
    }

    private static ICoopCommand CreateCommand(string name)
    {
        Type type = Assert.Single(GetCommandTypes(), candidate => Instantiate(candidate).Name == name);
        return Instantiate(type);
    }

    // Injected arguments are null, so only read metadata or IL from commands that take them.
    private static ICoopCommand Instantiate(Type type)
    {
        ConstructorInfo[] constructors = type.GetConstructors();
        Assert.True(constructors.Length == 1, $"{type.Name} needs exactly one public constructor");
        return (ICoopCommand)constructors[0].Invoke(new object[constructors[0].GetParameters().Length]);
    }

    private static Type[] GetCommandTypes()
    {
        return typeof(global::Missions.MissionModule).Assembly.GetTypes()
            .Where(type => type.IsClass &&
                           !type.IsAbstract &&
                           type.DeclaringType != null &&
                           OwningTypes.Contains(type.DeclaringType.Name) &&
                           typeof(ICoopCommand).IsAssignableFrom(type))
            .ToArray();
    }

    private sealed class TestArgs : ICoopCommandArgs
    {
        private readonly IReadOnlyList<string> values;

        public TestArgs(IReadOnlyList<string> values)
        {
            this.values = values;
        }

        public int Count => values.Count;

        public string this[int index] => values[index];

        public IEnumerator<string> GetEnumerator() => values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
