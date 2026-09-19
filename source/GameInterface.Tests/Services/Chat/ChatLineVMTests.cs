using GameInterface.Services.Chat;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Chat;

public class ChatLineVMTests
{
    [Theory]
    [InlineData(0f, false, 1f)]
    [InlineData(9.9f, false, 1f)]
    [InlineData(10f, false, 1f)]
    [InlineData(10.25f, false, 0.5f)]
    [InlineData(10.5f, false, 0f)]
    [InlineData(11f, false, 0f)]
    [InlineData(10.5f, true, 1f)]
    public void ComputeAlpha_MatchesVanillaFadeCurve(float time, bool forcedVisible, float expected)
    {
        Assert.Equal(expected, ChatLineVM.ComputeAlpha(time, forcedVisible), 3);
    }

    [Fact]
    public void HandleFading_ReducesAlphaAfterVisibilityWindow()
    {
        var line = new ChatLineVM("event", Color.White, isPlayerChat: false);

        line.HandleFading(10.25f);

        Assert.Equal(0.5f, line.Alpha, 3);
    }

    [Fact]
    public void ToggleForceVisible_KeepsFullAlphaWhileOpen()
    {
        var line = new ChatLineVM("event", Color.White, isPlayerChat: false);
        line.HandleFading(11f);
        Assert.Equal(0f, line.Alpha, 3);

        line.ToggleForceVisible(true);

        Assert.Equal(1f, line.Alpha, 3);
    }
}