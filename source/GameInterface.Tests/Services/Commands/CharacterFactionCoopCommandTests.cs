using Common.Commands;
using GameInterface.Services.Clans.Commands;
using GameInterface.Services.Heroes.Commands;
using GameInterface.Services.Kingdoms.Commands;
using GameInterface.Utils.Commands;
using Moq;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace GameInterface.Tests.Services.Commands;

public class CharacterFactionCoopCommandTests
{
    private readonly ILegacyCoopCommandExecutor executor = new LegacyCoopCommandExecutor();

    [Fact]
    public void MigratedCommands_HaveUniqueLowerSnakeCaseNamesAndSpecificInterfaces()
    {
        List<ICoopCommand> commands = GetMigratedCommands();

#if DEBUG
        Assert.Equal(134, commands.Count);
#else
        Assert.Equal(129, commands.Count);
#endif
        Assert.Equal(
            commands.Count,
            commands.Select(command => $"{command.Prefix}.{command.Name}").Distinct().Count());

        foreach (ICoopCommand command in commands)
        {
            Assert.Matches(new Regex("^[a-z0-9]+(?:_[a-z0-9]+)*$"), command.Name);
            Assert.False(string.IsNullOrWhiteSpace(command.Description));
            Assert.NotNull(command.ExpectedArgs);

            Type commandType = command.GetType();
            Assert.Contains(
                commandType.GetInterfaces(),
                interfaceType => interfaceType != typeof(ICoopCommand) &&
                                 typeof(ICoopCommand).IsAssignableFrom(interfaceType));
        }
    }

    [Fact]
    public void LogicalNames_RequireOneQuotedArgumentSlot()
    {
        var setGold = new HeroSetGoldCommand(executor);
        var createKingdom = new KingdomCreateCommand(executor);
        var clanEconomy = new ClanEconomyCommand(executor);

        Assert.Equal(2, setGold.ExpectedArgs.Length);
        Assert.Contains("Quote multi-word", setGold.ExpectedArgs[0].Description);
        Assert.Equal(2, createKingdom.ExpectedArgs.Length);
        Assert.All(createKingdom.ExpectedArgs, expected => Assert.Contains("Quote multi-word", expected.Description));
        Assert.Single(clanEconomy.ExpectedArgs);
        Assert.Contains("Quote multi-word", clanEconomy.ExpectedArgs[0].Description);

        AssertInvalidArguments(setGold, "Lady", "Isolla", "500");
        AssertInvalidArguments(createKingdom, "Rhagaea", "New", "Empire");
        AssertInvalidArguments(clanEconomy, "Southern", "Empire");
    }

    [Fact]
    public void ClanInfoNames_KeepSummaryAndSeparateFieldDump()
    {
        var info = new ClanInfoCommand(executor);
        var fieldDump = new ClanFieldDumpCommand(executor);

        Assert.Equal("info", info.Name);
        Assert.Contains("curated summary", info.Description);
        Assert.Equal("field_dump", fieldDump.Name);
        Assert.Contains("every field", fieldDump.Description);
    }

    [Fact]
    public void LegacyExecutor_ReturnsStructuredSuccessAndFailureResults()
    {
        CoopCommandResult success = executor.Execute(new TestArgs(Array.Empty<string>()), _ => "done");
        CoopCommandResult failure = executor.Execute(new TestArgs(Array.Empty<string>()), _ => "Unable to resolve hero.");

        Assert.True(success.Succeeded);
        Assert.Null(success.ErrorCode);
        Assert.False(failure.Succeeded);
        Assert.Equal("command_rejected", failure.ErrorCode);
    }

    private List<ICoopCommand> GetMigratedCommands()
    {
        return typeof(HeroSetGoldCommand).Assembly.GetTypes()
            .Where(type => type.IsClass &&
                           !type.IsAbstract &&
                           typeof(LegacyCoopCommand).IsAssignableFrom(type))
            .Select(type => (ICoopCommand)Activator.CreateInstance(type, executor))
            .ToList();
    }

    private static void AssertInvalidArguments(ICoopCommand command, params string[] values)
    {
        var registry = new CoopCommandRegistry(new[] { command }, Mock.Of<ILogger>());
        CoopCommandResult result = registry.ProcessCommand(
            $"{command.Prefix}.{command.Name}",
            new TestArgs(values));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_arguments", result.ErrorCode);
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

        public IEnumerator<string> GetEnumerator()
        {
            return values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
