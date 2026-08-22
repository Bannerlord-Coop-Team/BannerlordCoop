using Common;
using GameInterface.Services.GameDebug.Commands;
using System;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.GameDebug;

public class FpsDebugCommandTests : IDisposable
{
    public void Dispose() => FpsLogger.Enabled = false;

    [Fact]
    public void Fps_WhenOn_EnablesLogging()
    {
        FpsLogger.Enabled = false;

        string result = FpsDebugCommand.Fps(new List<string> { "on" });

        Assert.True(FpsLogger.Enabled);
        Assert.Contains("ON", result);
    }

    [Fact]
    public void Fps_WhenOff_DisablesLogging()
    {
        FpsLogger.Enabled = true;

        string result = FpsDebugCommand.Fps(new List<string> { "off" });

        Assert.False(FpsLogger.Enabled);
        Assert.Contains("OFF", result);
    }

    [Fact]
    public void Fps_WithNoArgument_TogglesCurrentState()
    {
        FpsLogger.Enabled = false;

        FpsDebugCommand.Fps(new List<string>());

        Assert.True(FpsLogger.Enabled);
    }

    [Theory]
    [InlineData("butter")]
    [InlineData("2")]
    public void Fps_WithInvalidArgument_ReturnsUsage(string arg)
    {
        string result = FpsDebugCommand.Fps(new List<string> { arg });

        Assert.StartsWith("Usage:", result);
    }
}
