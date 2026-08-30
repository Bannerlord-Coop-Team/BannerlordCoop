using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>
/// Guards the fix for issue #3388: an attached army member is permanently at the <c>army_wait</c> menu, so the
/// player-party interaction dialog (and therefore the barter screen) must still be allowed to open there.
/// </summary>
public class PlayerPartyMapConversationGateTests
{
    [Theory]
    [InlineData("army_wait")]
    [InlineData("army_wait_at_settlement")]
    [InlineData("army_encounter")]
    [InlineData("game_menu_army_talk_to_other_members")]
    public void IsBenignConversationMenu_ParkedArmyMenus_AreBenign(string menuId)
    {
        Assert.True(PlayerPartyInteractionHandler.IsBenignConversationMenu(menuId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("town")]
    [InlineData("village")]
    [InlineData("town_outside")]
    [InlineData("encounter")]
    [InlineData("army_dispersed")]
    [InlineData("menu_siege_strategies")]
    public void IsBenignConversationMenu_OtherMenus_AreNotBenign(string menuId)
    {
        Assert.False(PlayerPartyInteractionHandler.IsBenignConversationMenu(menuId));
    }

    [Fact]
    public void CanOpenMapConversation_OnOpenMap_IsAllowed()
    {
        Assert.True(PlayerPartyInteractionHandler.CanOpenMapConversation(
            atMenu: false, currentMenuId: null, topScreenIsMapScreen: true));
    }

    [Fact]
    public void CanOpenMapConversation_AtArmyWaitMenu_IsAllowed()
    {
        // The #3388 regression: army members sit at army_wait, and the blanket AtMenu bail soft-locked them.
        Assert.True(PlayerPartyInteractionHandler.CanOpenMapConversation(
            atMenu: true, currentMenuId: "army_wait", topScreenIsMapScreen: true));
    }

    [Fact]
    public void CanOpenMapConversation_AtArmyEncounterMenu_IsAllowed()
    {
        Assert.True(PlayerPartyInteractionHandler.CanOpenMapConversation(
            atMenu: true, currentMenuId: "army_encounter", topScreenIsMapScreen: true));
    }

    [Fact]
    public void CanOpenMapConversation_AtSettlementMenu_IsDeferred()
    {
        Assert.False(PlayerPartyInteractionHandler.CanOpenMapConversation(
            atMenu: true, currentMenuId: "town", topScreenIsMapScreen: true));
    }

    [Fact]
    public void CanOpenMapConversation_AtArmyWaitMenu_ButMapScreenNotOnTop_IsDeferred()
    {
        Assert.False(PlayerPartyInteractionHandler.CanOpenMapConversation(
            atMenu: true, currentMenuId: "army_wait", topScreenIsMapScreen: false));
    }

    [Fact]
    public void CanOpenMapConversation_OnOpenMap_ButMapScreenNotOnTop_IsDeferred()
    {
        Assert.False(PlayerPartyInteractionHandler.CanOpenMapConversation(
            atMenu: false, currentMenuId: null, topScreenIsMapScreen: false));
    }
}
