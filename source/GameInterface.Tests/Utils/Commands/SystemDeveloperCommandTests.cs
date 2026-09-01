using Common;
using Common.Commands;
using GameInterface.Services.SystemDeveloper.Commands;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Utils.Commands;

[Collection(ModInformationRoleCollection.Name)]
public class SystemDeveloperCommandTests
{
    [Fact]
    public void Commands_HaveValidUniqueNormalizedMetadata()
    {
        ICoopCommand[] commands = CreateCommands();
        var registry = new CoopCommandRegistry(commands, new LoggerConfiguration().CreateLogger());

#if DEBUG
        Assert.Equal(106, commands.Length);
#else
        Assert.Equal(95, commands.Length);
#endif
        Assert.Equal(commands.Length, registry.Commands.Count);
        Assert.All(commands, AssertNormalizedMetadata);
    }

    [Fact]
    public void MultiWordArguments_UseFixedLogicalSlots()
    {
        AssertArgumentNames(new HeroDeveloperAddSkillXpCommand(),
            "hero_name_or_id", "skill_name", "xp_amount");
        AssertArgumentNames(new PlayerCaptivityCapturePlayerCommand(),
            "hero_id", "captor_party_id");
        AssertArgumentNames(new GameThreadStallCommand(), "milliseconds");

        IExpectedArgs mode = new CampaignOptionsPlayerTroopsReceivedDamageCommand().ExpectedArgs.Single();
        Assert.False(mode.IsRequired);
    }

    [Fact]
    public void Registry_RejectsExtraUnquotedHeroNameTokens()
    {
        var command = new HeroDeveloperAddAttributePointsCommand();
        var registry = new CoopCommandRegistry(
            new[] { command },
            new LoggerConfiguration().CreateLogger());
        var argsFactory = new CoopCommandArgsFactory();

        CoopCommandResult result = registry.ProcessCommand(
            "coop.debug.hero_developer.add_attribute_points",
            argsFactory.FromValues(new[] { "Hero", "With", "Spaces", "2" }));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_arguments", result.ErrorCode);
    }

    [Fact]
    public void CampaignOptionRoleFailure_ReturnsFailedResultThroughRegistry()
    {
        bool wasServer = ModInformation.IsServer;
        ModInformation.IsServer = false;
        try
        {
            var command = new CampaignOptionsAutoAllocateClanMemberPerksCommand();
            var registry = new CoopCommandRegistry(
                new[] { command },
                new LoggerConfiguration().CreateLogger());
            var argsFactory = new CoopCommandArgsFactory();

            CoopCommandResult result = registry.ProcessCommand(
                "coop.debug.campaign_options.auto_allocate_clan_member_perks",
                argsFactory.FromValues(new[] { "true" }));

            Assert.False(result.Succeeded);
            Assert.Equal("command_rejected", result.ErrorCode);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Theory]
    [InlineData("Failed: no active campaign.", false)]
    [InlineData("Command can only be run on the server.", false)]
    [InlineData("Managing campaign options is disabled on clients; the host does this.", false)]
    [InlineData("An integer amount of xp is required.", false)]
    [InlineData("Hero not found.", false)]
    [InlineData("A save is already queued.", false)]
    [InlineData("Unknown quest type key 'missing'.", false)]
    [InlineData("Tactical unit symbols configuration is unavailable.", false)]
    [InlineData("Close screen failed.\nException: InvalidOperationException", false)]
    [InlineData("Diagnostic bug-report log sharing is disabled.", true)]
    [InlineData("Advanced campaign time forward by 2 days.", true)]
    public void LegacyResult_MapsSuccessAndFailure(string output, bool succeeded)
    {
        CoopCommandResult result = SystemDeveloperLegacyCommandResult.FromOutput(output);

        Assert.Equal(succeeded, result.Succeeded);
        Assert.Equal(succeeded ? null : "command_rejected", result.ErrorCode);
        Assert.Equal(output, result.Output);
    }

    [Fact]
    public void LegacyResult_CommandSpecificFailure_DoesNotChangeStatusResult()
    {
        const string output = "Steam integration inactive";

        CoopCommandResult hostResult = SystemDeveloperLegacyCommandResult.FromOutput(
            output,
            "Steam integration inactive");
        CoopCommandResult statusResult = SystemDeveloperLegacyCommandResult.FromOutput(output);

        Assert.False(hostResult.Succeeded);
        Assert.True(statusResult.Succeeded);
    }

    private static ICoopCommand[] CreateCommands()
    {
        return typeof(CampaignOptionsListCommand).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => type.Namespace == "GameInterface.Services.SystemDeveloper.Commands")
            .Where(type => typeof(ICoopCommand).IsAssignableFrom(type))
            .Select(type => (ICoopCommand)Activator.CreateInstance(type))
            .ToArray();
    }

    private static void AssertNormalizedMetadata(ICoopCommand command)
    {
        Assert.Matches("^[a-z0-9_]+(?:\\.[a-z0-9_]+)*$", command.Prefix);
        Assert.Matches("^[a-z0-9_]+$", command.Name);
        Assert.False(string.IsNullOrWhiteSpace(command.Description));
        Assert.NotNull(command.ExpectedArgs);

        bool optionalFound = false;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (IExpectedArgs expectedArg in command.ExpectedArgs)
        {
            Assert.Matches("^[a-z0-9_]+$", expectedArg.Name);
            Assert.True(names.Add(expectedArg.Name));
            Assert.False(string.IsNullOrWhiteSpace(expectedArg.Description));
            Assert.False(expectedArg.IsRequired && optionalFound);
            optionalFound |= !expectedArg.IsRequired;
        }
    }

    private static void AssertArgumentNames(ICoopCommand command, params string[] expected)
    {
        Assert.Equal(expected, command.ExpectedArgs.Select(argument => argument.Name));
    }
}
