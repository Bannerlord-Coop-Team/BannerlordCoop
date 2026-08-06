using GameInterface.Services.Chat;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;
using Xunit;

namespace GameInterface.Tests.Services.Chat;

public class ChatOverlayTests
{
    [Fact]
    public void PassiveInputRestrictions_LeaveKeyboardAndCursorToUnderlyingScreen()
    {
        var inputRestrictions = new InputRestrictions(900);

        ChatOverlay.SetPassiveInputRestrictions(inputRestrictions);

        Assert.Equal(InputUsageMask.Mouse, inputRestrictions.InputUsageMask);
        Assert.False(inputRestrictions.MouseVisibility);
    }

    [Fact]
    public void OutsideMouseClick_ReleasesOnlyFocusedTextInput()
    {
        Assert.True(ChatOverlay.ShouldReleaseInputFocus(
            inputFocused: true,
            mouseButtonPressed: true,
            inputHovered: false));
        Assert.False(ChatOverlay.ShouldReleaseInputFocus(
            inputFocused: true,
            mouseButtonPressed: true,
            inputHovered: true));
        Assert.False(ChatOverlay.ShouldReleaseInputFocus(
            inputFocused: true,
            mouseButtonPressed: false,
            inputHovered: false));
        Assert.False(ChatOverlay.ShouldReleaseInputFocus(
            inputFocused: false,
            mouseButtonPressed: true,
            inputHovered: false));
    }
}
