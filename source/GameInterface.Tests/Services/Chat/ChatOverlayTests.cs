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
    public void DisplayOnlyInputRestrictions_LeaveMouseToUnderlyingMenuScreens()
    {
        var inputRestrictions = new InputRestrictions(900);

        ChatOverlay.SetDisplayOnlyInputRestrictions(inputRestrictions);

        Assert.Equal(InputUsageMask.Invalid, inputRestrictions.InputUsageMask);
        Assert.False(inputRestrictions.MouseVisibility);
    }

    [Fact]
    public void OpenPanelInputRestrictions_ShowCursorWithoutClaimingKeyboard()
    {
        var inputRestrictions = new InputRestrictions(900);

        ChatOverlay.SetOpenPanelInputRestrictions(inputRestrictions);

        Assert.Equal(InputUsageMask.Mouse, inputRestrictions.InputUsageMask);
        Assert.True(inputRestrictions.MouseVisibility);
    }

    [Fact]
    public void OpenPanelTypingRestrictions_ClaimKeyboardWithoutMouse()
    {
        var inputRestrictions = new InputRestrictions(900);

        ChatOverlay.SetOpenPanelTypingRestrictions(inputRestrictions);

        Assert.Equal(InputUsageMask.Keyboardkeys, inputRestrictions.InputUsageMask);
        Assert.False(inputRestrictions.MouseVisibility);
    }

    [Fact]
    public void OpenPanelTypingWithMouseRestrictions_ClaimAllWithCursor()
    {
        var inputRestrictions = new InputRestrictions(900);

        ChatOverlay.SetOpenPanelTypingWithMouseRestrictions(inputRestrictions);

        Assert.Equal(InputUsageMask.All, inputRestrictions.InputUsageMask);
        Assert.True(inputRestrictions.MouseVisibility);
    }

    [Theory]
    [InlineData(false, true, true, true, true)]
    [InlineData(false, true, true, false, false)]
    [InlineData(false, true, false, true, false)]
    [InlineData(true, true, false, false, true)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, true, true, false, true)]
    public void ChatPointerCapture_StartsOnPressOverPanelOnly(
        bool currentlyHeld,
        bool buttonDown,
        bool buttonPressed,
        bool pointerOverChat,
        bool expected)
    {
        Assert.Equal(expected, ChatOverlay.ShouldKeepChatPointerCapture(
            currentlyHeld,
            buttonDown,
            buttonPressed,
            pointerOverChat));
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
    public void CloseInput_UsesCancelReleaseAndTogglePress(
        bool escapeReleased,
        bool controllerCancelReleased,
        bool controllerTogglePressed)
    {
        Assert.True(ChatOverlay.ShouldCloseInput(
            escapeReleased,
            controllerCancelReleased,
            controllerTogglePressed));
    }

    [Theory]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, false, false)]
    public void CloseKeyPress_CapturesPassiveChatUntilRelease(
        bool inputFocused,
        bool escapePressed,
        bool controllerCancelPressed,
        bool expected)
    {
        Assert.Equal(expected, ChatOverlay.ShouldCaptureCloseInput(
            inputFocused,
            escapePressed,
            controllerCancelPressed));
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, false, false)]
    public void Presentation_ShowsEventLogDuringCampaign(
        bool isCampaignContext,
        bool isConversationActive,
        bool isLoading,
        bool expected)
    {
        Assert.Equal(expected, ChatOverlay.ShouldShowPresentation(
            isCampaignContext,
            isConversationActive,
            isLoading));
    }

    [Theory]
    [InlineData(true, false, true, false, true)]
    [InlineData(true, false, false, true, true)]
    [InlineData(false, false, true, false, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, false, false, false, false)]
    public void ChatOpen_WhenOpenableScreenAndFocusAllows(
        bool isOpenableScreen,
        bool isConversationActive,
        bool isGameplayLayerFocused,
        bool isChatLayerFocused,
        bool expected)
    {
        Assert.Equal(expected, ChatOverlay.ShouldAllowChatOpen(
            isOpenableScreen,
            isConversationActive,
            isGameplayLayerFocused,
            isChatLayerFocused));
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    public void SettlementMapMenu_OnlyWhenMapMenuAndInSettlement(
        bool isMapScreen,
        bool atMenu,
        bool hasCurrentSettlement,
        bool expected)
    {
        Assert.Equal(expected, ChatOverlay.IsSettlementMapMenu(
            isMapScreen,
            atMenu,
            hasCurrentSettlement));
    }
}
