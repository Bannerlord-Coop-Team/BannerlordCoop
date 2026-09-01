using Common;
using Common.Commands;
using GameInterface.Services.MapEvents.Commands;
using GameInterface.Services.Party.Commands;
using GameInterface.Services.PartyVisuals.Commands;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Utils.Commands;

[Collection(global::GameInterface.Tests.ModInformationRoleCollection.Name)]
public class MigratedAttributedCommandTests
{
    private static readonly string[] CommandNamespaces =
    {
        "GameInterface.Services.MapEvents.Commands",
        "GameInterface.Services.Party.Commands",
        "GameInterface.Services.PartyVisuals.Commands",
    };

    [Fact]
    public void Commands_HaveValidUniqueMetadata()
    {
        ICoopCommand[] commands = CreateCommands();
        var logger = new LoggerConfiguration().CreateLogger();

        var registry = new CoopCommandRegistry(commands, logger);

#if DEBUG
        Assert.Equal(120, commands.Length);
#else
        Assert.Equal(101, commands.Length);
#endif
        Assert.Equal(commands.Length, registry.Commands.Count);
        Assert.All(commands, AssertNormalizedMetadata);
    }

    [Fact]
    public void QuotedLegacyArguments_AreRepresentedAsSingleRegistryArguments()
    {
        var resultFactory = new PartyLegacyCommandResult();
        AssertArgumentNames(new CharacterIdsCommand(resultFactory), "hero_name_or_id");
        AssertArgumentNames(new AddTroopXpCommand(resultFactory), "hero_name", "xp_amount");
        AssertArgumentNames(new ImprisonCompanionCommand(resultFactory), "captor_hero", "prisoner_hero");

        var optionalCommand = new StartNearestBanditAttackCommand(new MapEventLegacyCommandResult());
        AssertArgumentNames(optionalCommand, "controller_id", "excluded_party_id");
        Assert.False(optionalCommand.ExpectedArgs[1].IsRequired);
    }

    [Fact]
    public void Registry_RejectsInvalidCountBeforeCallingLegacyLogic()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var command = new CharacterIdsCommand(new PartyLegacyCommandResult());
        var registry = new CoopCommandRegistry(new[] { command }, logger);
        var argsFactory = new CoopCommandArgsFactory();

        CoopCommandResult missing = registry.ProcessCommand(
            "coop.debug.mobile_party.character_ids",
            argsFactory.FromValues(Array.Empty<string>()));
        CoopCommandResult extra = registry.ProcessCommand(
            "coop.debug.mobile_party.character_ids",
            argsFactory.FromValues(new[] { "Hero One", "extra" }));

        Assert.False(missing.Succeeded);
        Assert.Equal("invalid_arguments", missing.ErrorCode);
        Assert.Contains("<hero_name_or_id>", missing.Output);
        Assert.False(extra.Succeeded);
        Assert.Equal("invalid_arguments", extra.ErrorCode);
    }

    [Theory]
    [InlineData("Failed: no active mission.", false)]
    [InlineData("Run this command on the server.", false)]
    [InlineData("Battle start coordinator is unavailable", false)]
    [InlineData("Server rejected attack mission for map_event_42", false)]
    [InlineData("Local deployment finished, but the local player agent was not assigned.", false)]
    [InlineData("Saved map event map_event_42 did not finalize cleanly.", false)]
    [InlineData("Fixture already active for player-1.", false)]
    [InlineData("Killed 3 enemy agent(s).", true)]
    public void LegacyResult_MapsSuccessAndFailure(string output, bool succeeded)
    {
        CoopCommandResult result = new MapEventLegacyCommandResult().FromOutput(output);

        Assert.Equal(succeeded, result.Succeeded);
        Assert.Equal(succeeded ? null : "command_failed", result.ErrorCode);
        Assert.Equal(output, result.Output);
    }

    [Fact]
    public void GetEventResult_MapEventSummary_IsSuccessful()
    {
        const string output = "Map event id: map_event_42\r\n\r\nSummary:";

        CoopCommandResult result = new MapEventLegacyCommandResult().FromOutput(output);

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorCode);
        Assert.Equal(output, result.Output);
    }

    [Fact]
    public void BattleRewardCleanPreflightResult_IsSuccessful()
    {
        const string output = "Battle reward fixture preflight is already clean.";

        CoopCommandResult result = new MapEventLegacyCommandResult().FromOutput(output, output);

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorCode);
        Assert.Equal(output, result.Output);
    }

    [Theory]
    [InlineData("CLAN_PARTY_TRANSFER_REJECTED")]
    [InlineData("CLAN_PARTY_TRANSFER_NOT_COMMITTED")]
    [InlineData("PARTY_SCREEN_UPGRADE_REJECTED character=aserai_recruit")]
    [InlineData("Garrison party 'town_comp_ES1' was not found.")]
    [InlineData("Danustica does not belong to the local player's clan.")]
    [InlineData("Please enter an integer for wounded count.")]
    [InlineData("Applied prison snapshot to Raganvad; 1 hero prisoner(s) still present (companion-preserve wrongly kept them).")]
    public void PartyResult_Rejection_IsFailure(string output)
    {
        CoopCommandResult result = new PartyLegacyCommandResult().FromOutput(output);

        Assert.False(result.Succeeded);
        Assert.Equal("command_failed", result.ErrorCode);
        Assert.Equal(output, result.Output);
    }

    [Theory]
    [InlineData("CLAN_PARTY_TRANSFER_STAGED")]
    [InlineData("CLAN_PARTY_TRANSFER_COMMITTED")]
    public void ClanPartyTransferResult_Success_IsSuccessful(string output)
    {
        CoopCommandResult result = new PartyLegacyCommandResult().FromOutput(output);

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorCode);
        Assert.Equal(output, result.Output);
    }

    [Theory]
    [InlineData("Mobile party visual manager is unavailable.")]
    [InlineData("stage_over_limit_fixture must be run on the server.")]
    public void PartyVisualResult_Rejection_IsFailure(string output)
    {
        CoopCommandResult result = new PartyVisualLegacyCommandResult().FromOutput(output);

        Assert.False(result.Succeeded);
        Assert.Equal("command_failed", result.ErrorCode);
        Assert.Equal(output, result.Output);
    }

    [Fact]
    public void PartyVisualResult_BufferState_IsSuccessful()
    {
        const string output = "visualCount=3 bufferCapacity=128 dirtyCount=1";

        CoopCommandResult result = new PartyVisualLegacyCommandResult().FromOutput(output);

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorCode);
        Assert.Equal(output, result.Output);
    }

    [Fact]
    public void TypedCommandBoundaries_RoleRejections_AreFailures()
    {
        bool originalIsServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = true;
            var argsFactory = new CoopCommandArgsFactory();

            CoopCommandResult stageResult = new StageClanPartyTransferCommand(new PartyLegacyCommandResult())
                .ProcessCommand(argsFactory.FromValues(new[] { "clan-party", "character" }));
            CoopCommandResult commitResult = new CommitClanPartyTransferCommand(new PartyLegacyCommandResult())
                .ProcessCommand(argsFactory.FromValues(Array.Empty<string>()));
            CoopCommandResult bufferResult = new BufferStateCommand(new PartyVisualLegacyCommandResult())
                .ProcessCommand(argsFactory.FromValues(Array.Empty<string>()));
            CoopCommandResult woundedResult = new SetTroopWoundedCommand(new PartyLegacyCommandResult())
                .ProcessCommand(argsFactory.FromValues(new[] { "party", "character", "not-an-integer" }));

            Assert.False(stageResult.Succeeded);
            Assert.Equal("command_failed", stageResult.ErrorCode);
            Assert.Equal("Command can only be run on a client.", stageResult.Output);
            Assert.False(commitResult.Succeeded);
            Assert.Equal("command_failed", commitResult.ErrorCode);
            Assert.Equal("Command can only be run on a client.", commitResult.Output);
            Assert.False(bufferResult.Succeeded);
            Assert.Equal("command_failed", bufferResult.ErrorCode);
            Assert.Equal("Run this command on a client.", bufferResult.Output);
            Assert.False(woundedResult.Succeeded);
            Assert.Equal("command_failed", woundedResult.ErrorCode);
            Assert.Equal("Please enter an integer for wounded count.", woundedResult.Output);
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;
        }
    }

    [Fact]
    public void AddTroopXpResult_InvalidXp_IsFailure()
    {
        bool originalIsServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = true;
            var command = new AddTroopXpCommand(new PartyLegacyCommandResult());
            var argsFactory = new CoopCommandArgsFactory();

            CoopCommandResult result = command.ProcessCommand(
                argsFactory.FromValues(new[] { "Hero One", "not-an-integer" }));

            Assert.False(result.Succeeded);
            Assert.Equal("command_failed", result.ErrorCode);
            Assert.Equal("Please enter an integer for xp amount", result.Output);
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;
        }
    }

    private static ICoopCommand[] CreateCommands()
    {
        return typeof(BufferStateCommand).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(ICoopCommand).IsAssignableFrom(type))
            .Where(type => CommandNamespaces.Contains(type.Namespace))
            .Select(CreateCommand)
            .ToArray();
    }

    private static ICoopCommand CreateCommand(Type type)
    {
        object resultFactory = type.Namespace switch
        {
            "GameInterface.Services.MapEvents.Commands" => new MapEventLegacyCommandResult(),
            "GameInterface.Services.Party.Commands" => new PartyLegacyCommandResult(),
            "GameInterface.Services.PartyVisuals.Commands" => new PartyVisualLegacyCommandResult(),
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

    private static void AssertArgumentNames(ICoopCommand command, params string[] expected)
    {
        Assert.Equal(expected, command.ExpectedArgs.Select(argument => argument.Name));
    }
}
