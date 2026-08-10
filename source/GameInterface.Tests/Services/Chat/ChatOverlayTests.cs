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
    public void CloseInput_UsesCloseKeyRelease(
        bool escapeReleased,
        bool controllerCancelReleased,
        bool controllerToggleReleased)
    {
        Assert.True(ChatOverlay.ShouldCloseInput(
            escapeReleased,
            controllerCancelReleased,
            controllerToggleReleased));
    }

    [Theory]
    [InlineData(false, true, false, false, true)]
    [InlineData(false, false, true, false, true)]
    [InlineData(false, false, false, true, true)]
    [InlineData(true, true, false, false, false)]
    [InlineData(false, false, false, false, false)]
    public void CloseKeyPress_CapturesPassiveChatUntilRelease(
        bool inputFocused,
        bool escapePressed,
        bool controllerCancelPressed,
        bool controllerTogglePressed,
        bool expected)
    {
        Assert.Equal(expected, ChatOverlay.ShouldCaptureCloseInput(
            inputFocused,
            escapePressed,
            controllerCancelPressed,
            controllerTogglePressed));
    }

    [Theory]
    [InlineData(true, true, false, true, false, true)]
    [InlineData(true, true, false, false, true, true)]
    [InlineData(false, true, false, true, false, false)]
    [InlineData(true, false, false, true, false, false)]
    [InlineData(true, true, true, true, false, false)]
    [InlineData(true, true, true, false, true, false)]
    [InlineData(true, true, false, false, false, false)]
    public void Presentation_IsLimitedToUnobstructedGameplay(
        bool isEnabled,
        bool isGameplayScreen,
        bool isConversationActive,
        bool isGameplayLayerFocused,
        bool isChatLayerFocused,
        bool expected)
    {
        Assert.Equal(expected, ChatOverlay.ShouldShowPresentation(
            isEnabled,
            isGameplayScreen,
            isConversationActive,
            isGameplayLayerFocused,
            isChatLayerFocused));
    }
}
