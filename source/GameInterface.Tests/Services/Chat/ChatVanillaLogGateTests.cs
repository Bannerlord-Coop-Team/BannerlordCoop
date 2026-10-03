using GameInterface.Services.Chat;
using Xunit;

namespace GameInterface.Tests.Services.Chat;

public class ChatVanillaLogGateTests
{
    [Fact]
    public void SetReplacementVisible_TracksVisibilityWithoutRequiringVanillaView()
    {
        var gate = new ChatVanillaLogGate();

        gate.SetReplacementVisible(true);
        Assert.True(ChatVanillaLogGate.IsReplacementVisible);

        gate.SetReplacementVisible(false);
        Assert.False(ChatVanillaLogGate.IsReplacementVisible);
    }
}
