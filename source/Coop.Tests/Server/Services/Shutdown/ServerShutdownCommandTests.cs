using Common;
using Common.Commands;
using Coop.Core.Server.Services.Shutdown;
using Coop.Core.Server.Services.Shutdown.Commands;
using Moq;
using Serilog;
using Xunit;

namespace Coop.Tests.Server.Services.Shutdown;

[Collection(ModInformationRoleCollection.Name)]
public class ServerShutdownCommandTests
{
    private const string FullName = "coop.server.shutdown";

    private readonly Mock<IServerShutdownCoordinator> coordinator = new Mock<IServerShutdownCoordinator>(MockBehavior.Strict);

    [Fact]
    public void Registry_OnAClient_RefusesWithWrongSide()
    {
        var registry = new CoopCommandRegistry(
            new ICoopCommand[] { new ServerShutdownCommand.ShutdownCoopCommand(coordinator.Object) },
            new LoggerConfiguration().CreateLogger());

        CoopCommandResult result = RunAs(isServer: false, () => registry.ProcessCommand(FullName, Args("60")));

        Assert.False(result.Succeeded);
        Assert.Equal("command_wrong_side", result.ErrorCode);
        coordinator.VerifyNoOtherCalls();
    }

    [Fact]
    public void Registry_ListsAServerOnlyCommandWithOneOptionalArgument()
    {
        var registry = new CoopCommandRegistry(
            new ICoopCommand[] { new ServerShutdownCommand.ShutdownCoopCommand(coordinator.Object) },
            new LoggerConfiguration().CreateLogger());

        CoopCommandDescriptor descriptor = Assert.Single(registry.Commands);
        Assert.Equal(FullName, descriptor.FullName);
        Assert.Equal(CoopCommandSide.Server, descriptor.Side);
        Assert.Equal(new[] { true, false }, System.Array.ConvertAll(descriptor.ExpectedArgs, arg => arg.IsRequired));
    }

    [Fact]
    public void Process_OnAClient_Fails()
    {
        CoopCommandResult result = RunAs(isServer: false, () => Process("60"));

        Assert.False(result.Succeeded);
        coordinator.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("+5")]
    [InlineData("1.5")]
    [InlineData("3601")]
    [InlineData("99999999999")]
    public void Process_BadSeconds_FailsWithoutScheduling(string seconds)
    {
        CoopCommandResult result = RunAs(isServer: true, () => Process(seconds));

        Assert.False(result.Succeeded);
        Assert.Equal("command_failed", result.ErrorCode);
        coordinator.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(new[] { "0" }, 0, null)]
    [InlineData(new[] { "3600" }, 3600, null)]
    [InlineData(new[] { "75", "MP" }, 75, "MP")]
    public void Process_Seconds_Schedules(string[] args, int seconds, string? saveName)
    {
        string scheduled = "scheduled";
        coordinator.Setup(value => value.TrySchedule(seconds, saveName!, out scheduled)).Returns(true);

        CoopCommandResult result = RunAs(isServer: true, () => Process(args));

        Assert.True(result.Succeeded);
        Assert.Equal("scheduled", result.Output);
    }

    [Fact]
    public void Process_RefusedSchedule_Fails()
    {
        string refused = "refused";
        coordinator.Setup(value => value.TrySchedule(60, null!, out refused)).Returns(false);

        CoopCommandResult result = RunAs(isServer: true, () => Process("60"));

        Assert.False(result.Succeeded);
        Assert.Equal("refused", result.Output);
    }

    [Fact]
    public void Process_Cancel_Cancels()
    {
        string cancelled = "cancelled";
        coordinator.Setup(value => value.TryCancel(out cancelled)).Returns(true);

        CoopCommandResult result = RunAs(isServer: true, () => Process("cancel"));

        Assert.True(result.Succeeded);
        Assert.Equal("cancelled", result.Output);
    }

    [Fact]
    public void Process_Status_Describes()
    {
        coordinator.Setup(value => value.DescribeStatus()).Returns("phase=Idle");

        CoopCommandResult result = RunAs(isServer: true, () => Process("status"));

        Assert.True(result.Succeeded);
        Assert.Equal("phase=Idle", result.Output);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("cancel")]
    public void Process_StatusOrCancelWithASaveName_FailsWithUsage(string action)
    {
        CoopCommandResult result = RunAs(isServer: true, () => Process(action, "MP"));

        Assert.False(result.Succeeded);
        Assert.Contains("Usage", result.Output);
        coordinator.VerifyNoOtherCalls();
    }

    private CoopCommandResult Process(params string[] args) =>
        new ServerShutdownCommand.ShutdownCoopCommand(coordinator.Object).ProcessCommand(Args(args));

    private static ICoopCommandArgs Args(params string[] values) => new CoopCommandArgsFactory().FromValues(values);

    private static CoopCommandResult RunAs(bool isServer, System.Func<CoopCommandResult> run)
    {
        bool wasServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = isServer;
            return run();
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
