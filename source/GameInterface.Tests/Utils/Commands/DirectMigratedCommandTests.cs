using Common;
using Common.Commands;
using GameInterface;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Utils.Commands;

[Collection(global::GameInterface.Tests.ModInformationRoleCollection.Name)]
public class DirectMigratedCommandTests
{
    private static readonly HashSet<string> OwningTypes = new HashSet<string>
    {
        "BattleTeamKillCommands",
        "KillPlayerAgentCommands",
        "MapEventDebugCommands",
        "PostBattleFreezeFixtureCommands",
        "GarrisonTroopXpFixtureCommands",
        "LargeBattleRosterFixtureCommands",
        "PartyCommands",
        "TroopXpTransferFixtureCommands",
        "PartyVisualDebugCommands",
    };

    [Fact]
    public void MigratedCommands_ReplaceAllAttributedMethodsWithDirectCommands()
    {
        Type[] commandTypes = GetCommandTypes();

#if DEBUG
        Assert.Equal(120, commandTypes.Length);
#else
        Assert.Equal(101, commandTypes.Length);
#endif
        Assert.All(commandTypes, type =>
        {
            Assert.Equal(typeof(object), type.BaseType);
            Assert.Equal(new[] { typeof(ICoopCommand) }, type.GetInterfaces());
            Assert.EndsWith("CoopCommand", type.Name);
        });

        MethodInfo[] attributedMethods = typeof(GameInterfaceModule).Assembly.GetTypes()
            .Where(type => OwningTypes.Contains(type.Name))
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.IsDefined(
                typeof(CommandLineFunctionality.CommandLineArgumentFunction), inherit: false))
            .ToArray();

        Assert.Empty(attributedMethods);
    }

    [Fact]
    public void MigratedCommands_HaveUniqueNormalizedMetadata()
    {
        ICoopCommand[] commands = CreateCommands();
        var registry = new CoopCommandRegistry(commands, new LoggerConfiguration().CreateLogger());

        Assert.Equal(commands.Length, registry.Commands.Count);
        Assert.Equal(
            commands.Length,
            commands.Select(command => $"{command.Prefix}.{command.Name}").Distinct().Count());
        Assert.All(commands, command =>
        {
            Assert.Matches("^coop(?:\\.[a-z0-9_]+)+$", command.Prefix);
            Assert.Matches("^[a-z0-9]+(?:_[a-z0-9]+)*$", command.Name);
            Assert.False(string.IsNullOrWhiteSpace(command.Description));
            Assert.NotNull(command.ExpectedArgs);
            Assert.All(command.ExpectedArgs, expectedArg =>
            {
                Assert.Matches("^[a-z0-9]+(?:_[a-z0-9]+)*$", expectedArg.Name);
                Assert.False(string.IsNullOrWhiteSpace(expectedArg.Description));
            });
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

        Assert.Equal(
            new[]
            {
                "battle_reward_fixture_start",
                "kms",
                "start_nearest_bandit_attack",
                "upgrade_party_screen_troop",
            },
            countReaders);
    }

    [Fact]
    public void Registry_RejectsInvalidArgumentCountBeforeCommandLogic()
    {
        ICoopCommand command = Assert.Single(
            CreateCommands(),
            candidate => candidate.Name == "set_troop_state");
        var registry = new CoopCommandRegistry(
            new[] { command },
            new LoggerConfiguration().CreateLogger());

        CoopCommandResult result = registry.ProcessCommand(
            $"{command.Prefix}.{command.Name}",
            new TestArgs(Array.Empty<string>()));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.Contains("<party_id>", result.Output);
    }

    [Theory]
    [InlineData("set_troop_wounded", "party", "troop", "not-an-integer")]
    [InlineData("stage_clan_party_transfer", "party", "troop")]
    public void ExplicitRejectionBranches_ReturnFailures(string commandName, params string[] args)
    {
        bool originalIsServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = true;
            ICoopCommand command = Assert.Single(
                CreateCommands(),
                candidate => candidate.Name == commandName);

            CoopCommandResult result = command.ProcessCommand(new TestArgs(args));

            Assert.False(result.Succeeded);
            Assert.Equal("command_failed", result.ErrorCode);
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;
        }
    }

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

    private static Type[] GetCommandTypes()
    {
        return typeof(GameInterfaceModule).Assembly.GetTypes()
            .Where(type => type.IsClass &&
                           !type.IsAbstract &&
                           type.DeclaringType != null &&
                           OwningTypes.Contains(type.DeclaringType.Name) &&
                           typeof(ICoopCommand).IsAssignableFrom(type))
            .ToArray();
    }

    private static ICoopCommand[] CreateCommands()
    {
        return GetCommandTypes()
            .Select(type => (ICoopCommand)Activator.CreateInstance(type))
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
