#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.Party.Commands;
using Serilog;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.Party;

[Collection(global::GameInterface.Tests.ModInformationRoleCollection.Name)]
public class PrisonerXpFixtureCommandsTests
{
    [Fact]
    public void SetPrisonerXp_RegistersAsServerCommand()
    {
        var command = new PrisonerXpFixtureCommands.SetPrisonerXpCoopCommand();
        var registry = new CoopCommandRegistry(new ICoopCommand[] { command }, new LoggerConfiguration().CreateLogger());

        Assert.Single(registry.Commands);
        Assert.Equal("coop.debug.mobile_party.set_prisoner_xp", $"{command.Prefix}.{command.Name}");
        Assert.Equal(CoopCommandSide.Server, command.Side);
        Assert.Equal(new[] { "party_id", "character_id", "xp" }, command.ExpectedArgs.Select(arg => arg.Name));
    }

    [Theory]
    [InlineData(true, "-1", "Xp must be a non-negative integer.")]
    [InlineData(true, "many", "Xp must be a non-negative integer.")]
    [InlineData(false, "100", "Command can only be run on the server.")]
    public void SetPrisonerXp_FailsBeforeAnyLookup(bool isServer, string xp, string expected)
    {
        bool previous = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = isServer;
            var result = new PrisonerXpFixtureCommands.SetPrisonerXpCoopCommand()
                .ProcessCommand(new CoopCommandArgsFactory().FromValues(new[] { "party", "character", xp }));

            Assert.False(result.Succeeded);
            Assert.Equal("command_failed", result.ErrorCode);
            Assert.Equal(expected, result.Output);
        }
        finally
        {
            ModInformation.IsServer = previous;
        }
    }
}
#endif
