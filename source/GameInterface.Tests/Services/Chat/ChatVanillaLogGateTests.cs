using GameInterface.Services.Chat;
using Xunit;

namespace GameInterface.Tests.Services.Chat;

public class ChatVanillaLogGateTests
{
    [Fact]
    public void ActivateAndDeactivate_ToggleIsActiveWithoutRequiringVanillaView()
    {
        var gate = new ChatVanillaLogGate();

        gate.Activate();
        Assert.True(ChatVanillaLogGate.IsActive);
        Assert.False(ChatVanillaLogGate.IsReplacementVisible);

        gate.Deactivate();
        Assert.False(ChatVanillaLogGate.IsActive);
        Assert.False(ChatVanillaLogGate.IsReplacementVisible);
    }

    [Fact]
    public void SetReplacementVisible_OnlyTracksWhileActive()
    {
        var gate = new ChatVanillaLogGate();

        gate.SetReplacementVisible(true);
        Assert.False(ChatVanillaLogGate.IsReplacementVisible);

        gate.Activate();
        gate.SetReplacementVisible(true);
        Assert.True(ChatVanillaLogGate.IsReplacementVisible);

        gate.SetReplacementVisible(false);
        Assert.False(ChatVanillaLogGate.IsReplacementVisible);

        gate.Deactivate();
        Assert.False(ChatVanillaLogGate.IsReplacementVisible);
    }
}
