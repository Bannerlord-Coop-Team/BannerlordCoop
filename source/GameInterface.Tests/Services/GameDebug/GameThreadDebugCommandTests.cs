using Common;
using GameInterface.Services.GameDebug.Commands;
using System;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.GameDebug;

[Collection(global::GameInterface.Tests.ModInformationRoleCollection.Name)]
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
            GameThreadDebugCommand.Stall(new List<string> { "1" }));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("5001")]
    [InlineData("invalid")]
    public void Stall_WhenDurationIsInvalid_ReturnsUsage(string duration)
    {
        ModInformation.IsServer = true;

        Assert.StartsWith(
            "Usage:",
            GameThreadDebugCommand.Stall(new List<string> { duration }));
    }

    [Fact]
    public void Stall_WhenServerAndDurationIsValid_StallsGameThread()
    {
        ModInformation.IsServer = true;

        Assert.Equal(
            "Stalled the server game thread for 1 ms",
            GameThreadDebugCommand.Stall(new List<string> { "1" }));
    }
}
