#if DEBUG
using Common;
using Common.Commands;
using GameInterface.Services.Armies.Commands;
using Xunit;

namespace GameInterface.Tests.Services.Armies;

[Collection(global::GameInterface.Tests.ModInformationRoleCollection.Name)]
public class ArmyObjectiveFixtureCommandTests
{
    [Theory]
    [InlineData(false, "prepare", "Run on the server.")]
    [InlineData(true, "restore", "No captured fixture.")]
    [InlineData(true, "state", "No captured fixture.")]
    [InlineData(true, "other", "Expected prepare, state, or restore.")]
    public void RefusesUnsafeOperationsBeforeAccessingCampaign(bool isServer, string operation, string expected)
    {
        var previous = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = isServer;
            var command = new ArmyObjectiveFixtureCommand(null, null);
            var result = command.ProcessCommand(new CoopCommandArgsFactory().FromValues(new[] { operation }));
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
