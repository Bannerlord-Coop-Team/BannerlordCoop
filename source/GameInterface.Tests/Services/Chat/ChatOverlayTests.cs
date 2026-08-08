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

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void OpenInput_UsesKeyboardAndNativeControllerChatKeys(
        bool enterPressed,
        bool numpadEnterPressed,
        bool controllerOpenPressed)
    {
        Assert.True(ChatOverlay.ShouldOpenInput(
            enterPressed,
            numpadEnterPressed,
            controllerOpenPressed));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void CloseInput_IsAvailableWheneverChatIsOpen(
        bool escapePressed,
        bool controllerCancelPressed,
        bool controllerTogglePressed)
    {
        Assert.True(ChatOverlay.ShouldCloseInput(
            escapePressed,
            controllerCancelPressed,
            controllerTogglePressed));
    }
}
