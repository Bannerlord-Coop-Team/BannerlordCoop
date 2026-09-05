using Common;
using Common.Commands;
using GameInterface;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Commands;

[Collection(global::GameInterface.Tests.ModInformationRoleCollection.Name)]
public class CharacterFactionCoopCommandTests
{
    private static readonly HashSet<string> OwningTypes = new HashSet<string>
    {
        "AlleyDebugCommand",
        "AlleyRecruitDebugCommand",
        "ArmyDebugCommand",
        "ClanDebugCommands",
        "CompanionsCommands",
        "HeroBoostFighterDebugCommand",
        "HeroConversationDebugCommand",
        "HeroDebugCommand",
        "KingdomDebugCommand",
        "RomanceDebugCommand",
        "DeletePlayerCommand",
        "PlayerDebugCommands",
    };

    [Fact]
    public void MigratedCommands_AreDirectCommandsInTheirOwningTypes()
    {
        Type[] commandTypes = GetCommandTypes();

#if DEBUG
        Assert.Equal(138, commandTypes.Length);
#else
        Assert.Equal(133, commandTypes.Length);
#endif
        Assert.All(commandTypes, type =>
        {
            Assert.Equal(typeof(object), type.BaseType);
            Assert.Equal(new[] { typeof(ICoopCommand) }, type.GetInterfaces());
            Assert.NotNull(type.DeclaringType);
            Assert.Contains(type.DeclaringType.Name, OwningTypes);
            Assert.EndsWith("CoopCommand", type.Name);
        });
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
            Assert.Matches("^coop(?:\\.[a-z0-9_]+)*$", command.Prefix);
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
    public void TargetedAttributedMethods_AreReplaced()
    {
        MethodInfo[] attributedMethods = typeof(GameInterfaceModule).Assembly.GetTypes()
            .Where(type => OwningTypes.Contains(type.Name))
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.IsDefined(
                typeof(CommandLineFunctionality.CommandLineArgumentFunction), inherit: false))
            .ToArray();

        Assert.Equal(
            new[] { "ForceAlly", "ForceTradeAgreement" },
            attributedMethods.Select(method => method.Name).OrderBy(name => name).ToArray());
    }

    [Fact]
    public void LogicalNames_UseFixedQuotedArgumentSlots()
    {
        ICoopCommand setGold = CreateCommand("coop.debug.hero", "set_gold");
        ICoopCommand createKingdom = CreateCommand("coop.debug.kingdom", "create");
        ICoopCommand clanEconomy = CreateCommand("coop.debug.clan", "economy");

        Assert.Equal(2, setGold.ExpectedArgs.Length);
        Assert.Contains("Quote multi-word", setGold.ExpectedArgs[0].Description);
        Assert.Equal(2, createKingdom.ExpectedArgs.Length);
        Assert.All(createKingdom.ExpectedArgs, expected => Assert.Contains("Quote multi-word", expected.Description));
        Assert.Single(clanEconomy.ExpectedArgs);
        Assert.Contains("Quote multi-word", clanEconomy.ExpectedArgs[0].Description);
    }

    [Fact]
    public void ClanInfoNames_KeepSummaryAndSeparateFieldDump()
    {
        ICoopCommand info = CreateCommand("coop.debug.clan", "info");
        ICoopCommand fieldDump = CreateCommand("coop.debug.clan", "field_dump");

        Assert.Contains("curated summary", info.Description);
        Assert.Contains("every field", fieldDump.Description);
    }

    [Fact]
    public void KingdomAddDecisionUsage_DescribesFixedDecisionSlots()
    {
        ICoopCommand command = CreateCommand("coop.debug.kingdom", "add_decision_usage");

        CoopCommandResult result = command.ProcessCommand(new TestArgs(Array.Empty<string>()));

        Assert.True(result.Succeeded);
        Assert.Contains("[decisionArg1] [decisionArg2] [decisionArg3]", result.Output);
        Assert.DoesNotContain("decisionTypeArgs", result.Output);
    }

    [Fact]
    public void Registry_RejectsInvalidArgumentCountBeforeCommandLogic()
    {
        ICoopCommand command = CreateCommand("coop.debug.hero", "set_gold");
        var registry = new CoopCommandRegistry(
            new[] { command },
            new LoggerConfiguration().CreateLogger());

        CoopCommandResult result = registry.ProcessCommand(
            $"{command.Prefix}.{command.Name}",
            new TestArgs(Array.Empty<string>()));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.Contains("<hero_name>", result.Output);
    }

    [Theory]
    [InlineData("coop.debug.army", "create", "empire", "town_ES1", "hero", "Raider")]
    [InlineData("coop.debug.romance", "marry", "player", "npc")]
    public void ServerCommands_RunOnClient_ReturnExplicitFailures(
        string prefix,
        string name,
        params string[] args)
    {
        bool originalIsServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = false;
            CoopCommandResult result = CreateCommand(prefix, name).ProcessCommand(new TestArgs(args));

            Assert.False(result.Succeeded);
            Assert.Equal("command_failed", result.ErrorCode);
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;
        }
    }

    private static ICoopCommand CreateCommand(string prefix, string name) =>
        Assert.Single(CreateCommands(), command => command.Prefix == prefix && command.Name == name);

    private static Type[] GetCommandTypes() =>
        typeof(GameInterfaceModule).Assembly.GetTypes()
            .Where(type => type.IsClass &&
                           !type.IsAbstract &&
                           type.DeclaringType != null &&
                           OwningTypes.Contains(type.DeclaringType.Name) &&
                           typeof(ICoopCommand).IsAssignableFrom(type))
            .ToArray();

    private static ICoopCommand[] CreateCommands() =>
        GetCommandTypes()
            .Select(type => (ICoopCommand)Activator.CreateInstance(type))
            .ToArray();

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
