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

        gate.Deactivate();
        Assert.False(ChatVanillaLogGate.IsActive);
    }
}