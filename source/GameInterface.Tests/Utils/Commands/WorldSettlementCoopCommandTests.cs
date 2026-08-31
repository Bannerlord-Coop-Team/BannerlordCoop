using Common.Commands;
using GameInterface.Utils.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace GameInterface.Tests.Utils.Commands;

public class WorldSettlementCoopCommandTests
{
    private static readonly string[] ScopedNamespaces =
    {
        "GameInterface.Services.Arenas.Commands",
        "GameInterface.Services.BesiegerCamps.Commands",
        "GameInterface.Services.Locations.Commands",
        "GameInterface.Services.MobileParties.Commands",
        "GameInterface.Services.Settlements.Commands",
        "GameInterface.Services.SiegeEvents.Commands",
        "GameInterface.Services.Tournaments.Commands",
        "GameInterface.Services.Towns.Commands",
        "GameInterface.Services.Villages.Commands",
        "GameInterface.Services.Workshops.Commands",
    };

    [Fact]
    public void ScopedCommands_ExposeNormalizedCompleteMetadata()
    {
        var commands = GetScopedCommands();
#if DEBUG
        Assert.Equal(134, commands.Count);
#else
        Assert.Equal(123, commands.Count);
#endif
        Assert.Equal(commands.Count, commands.Select(command => $"{command.Prefix}.{command.Name}").Distinct().Count());

        foreach (ICoopCommand command in commands)
        {
            Assert.Matches("^coop(?:\\.[a-z0-9_]+)+$", command.Prefix);
            Assert.Matches("^[a-z0-9]+(?:_[a-z0-9]+)*$", command.Name);
            Assert.False(string.IsNullOrWhiteSpace(command.Description));
            Assert.NotNull(command.ExpectedArgs);
            Assert.All(command.ExpectedArgs, expectedArg =>
            {
                Assert.Matches("^[a-z][A-Za-z0-9]*$", expectedArg.Name);
                Assert.False(string.IsNullOrWhiteSpace(expectedArg.Description));
            });
        }
    }

    [Fact]
    public void FormerNameJoiningCommands_RequireOneQuotedLogicalArgument()
    {
        AssertSingleQuotedNameArgument("coop.debug.town.refresh_mercenary_stocks");
        AssertSingleQuotedNameArgument("coop.debug.town.request_mercenary_stock");
        AssertSingleQuotedNameArgument("coop.debug.town.management_data");
        AssertSingleQuotedNameArgument("coop.debug.tournaments.add_tournament_to_town");
    }

    [Theory]
    [InlineData("coop.debug.besiegercamp.set_progress")]
    [InlineData("coop.debug.besiegercamp.add_besieger_party")]
    [InlineData("coop.debug.mobileparty.create_party")]
    [InlineData("coop.debug.mobileparty.destroy_all_bandit_parties")]
    [InlineData("coop.debug.settlement_component.set_owner")]
    [InlineData("coop.debug.settlements.set_owner_clan")]
    [InlineData("coop.debug.town.set_food_stocks")]
    public void MalformedLegacyNames_AreNormalized(string fullName)
    {
        Assert.Contains(GetScopedCommands(), command => $"{command.Prefix}.{command.Name}" == fullName);
    }

    [Fact]
    public void ScopedLegacyMethods_HaveNoConsoleCommandAttributes()
    {
        var attributedMethods = typeof(GameInterfaceModule).Assembly
            .GetTypes()
            .Where(type => ScopedNamespaces.Contains(type.Namespace))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Where(method => method.CustomAttributes.Any(attribute =>
                attribute.AttributeType.Name == "CommandLineArgumentFunctionAttribute"))
            .ToList();

        Assert.Empty(attributedMethods);
    }

    [Theory]
    [InlineData("Updated state", true)]
    [InlineData("No characters", true)]
    [InlineData("Usage: coop.debug.test <value>", false)]
    [InlineData("Town 'Danustica' not found", false)]
    public void LegacyResultMapping_ReportsCommandOutcome(string output, bool expected)
    {
        Assert.Equal(expected, LegacyCoopCommand.LegacyCommandSucceeded(output));
    }

    private static List<ICoopCommand> GetScopedCommands()
    {
        return typeof(GameInterfaceModule).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(LegacyCoopCommand).IsAssignableFrom(type))
            .Where(type => ScopedNamespaces.Contains(type.Namespace))
            .Select(type => (ICoopCommand)Activator.CreateInstance(type))
            .ToList();
    }

    private static void AssertSingleQuotedNameArgument(string fullName)
    {
        ICoopCommand command = Assert.Single(GetScopedCommands(), candidate =>
            $"{candidate.Prefix}.{candidate.Name}" == fullName);
        IExpectedArgs expectedArg = Assert.Single(command.ExpectedArgs);
        Assert.Contains("quote", expectedArg.Description, StringComparison.OrdinalIgnoreCase);
    }
}
