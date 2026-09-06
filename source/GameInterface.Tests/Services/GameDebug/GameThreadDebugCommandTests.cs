using Common;
using Common.Commands;
using GameInterface.Services.GameDebug.Commands;
using GameInterface.Tests;
using System;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.GameDebug;

[Collection(ModInformationRoleCollection.Name)]
public class GameThreadDebugCommandTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;

    public void Dispose()
    {
        ModInformation.IsServer = wasServer;
    }

    [Fact]
    public void Stall_WhenClient_ReturnsServerOnlyError()
    {
        ModInformation.IsServer = false;

        Assert.Equal(
            "gamethread.stall must be run on the server",
            Stall(new List<string> { "1" }));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("5001")]
    [InlineData("invalid")]
    public void Stall_WhenDurationIsInvalid_ReturnsUsage(string duration)
    {
        ModInformation.IsServer = true;

        Assert.StartsWith(
            "Stall duration must be",
            Stall(new List<string> { duration }));
    }

    [Fact]
    public void Stall_WhenServerAndDurationIsValid_StallsGameThread()
    {
        ModInformation.IsServer = true;

        Assert.Equal(
            "Stalled the server game thread for 1 ms",
            Stall(new List<string> { "1" }));
    }
    private static string Stall(List<string> args)
    {
        var command = new GameThreadDebugCommand.GameThreadStallCoopCommand();
        return command.ProcessCommand(new CoopCommandArgsFactory().FromValues(args)).Output;
    }
}
