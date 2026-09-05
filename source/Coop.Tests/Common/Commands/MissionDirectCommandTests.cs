using Common.Commands;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Coop.Tests.Commands;

public class MissionDirectCommandTests
{
    private static readonly HashSet<string> OwningTypes = new HashSet<string>
    {
        "MovementDebugCommands",
        "BattleDebugCommands",
    };

    [Fact]
    public void MissionCommands_AreDirectNormalizedCommands()
    {
        Type[] commandTypes = GetCommandTypes();
#if DEBUG
        Assert.Equal(29, commandTypes.Length);
#else
        Assert.Equal(15, commandTypes.Length);
#endif
        ICoopCommand[] commands = commandTypes
            .Select(type => (ICoopCommand)Activator.CreateInstance(type))
            .ToArray();
        var registry = new CoopCommandRegistry(commands, new LoggerConfiguration().CreateLogger());

        Assert.Equal(commands.Length, registry.Commands.Count);
#if DEBUG
        Assert.Contains(commands, command => command.Name == "peer_state");
        Assert.Contains(commands, command => command.Name == "controller_agents");
        Assert.Contains(commands, command => command.Name == "drive_owned_agents");
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
        string[] countReaders = GetCommandTypes()
            .Where(type => CallsArgumentCount(type.GetMethod(nameof(ICoopCommand.ProcessCommand))))
            .Select(type => ((ICoopCommand)Activator.CreateInstance(type)).Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(new[] { "ladder_state", "mount_state" }, countReaders);
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
        Type type = Assert.Single(GetCommandTypes(), candidate =>
        {
            var command = (ICoopCommand)Activator.CreateInstance(candidate);
            return command.Name == name;
        });
        return (ICoopCommand)Activator.CreateInstance(type);
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
