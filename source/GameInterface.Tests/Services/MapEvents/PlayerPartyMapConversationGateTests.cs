using Common.Util;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using GameInterface.Tests.Services.SiegeEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>
/// An attached army member is permanently at the army_wait menu, so the player-party interaction dialog (and
/// therefore the barter screen) must still be allowed to open there rather than being deferred like a
/// transient menu.
/// </summary>
[Collection(nameof(CampaignCurrentCollection))]
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
        // Army members sit at army_wait; a blanket AtMenu bail would soft-lock their interaction dialog.
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

    [Fact]
    public void CanOpenMapConversation_LiveEncounterWithSomeoneElse_IsDeferred()
    {
        // Player is parked at army_encounter for an unrelated army when another player initiates an interaction.
        // Opening the dialog here would let session teardown tear that encounter down.
        Assert.False(PlayerPartyInteractionHandler.CanOpenMapConversation(
            atMenu: true,
            currentMenuId: "army_encounter",
            topScreenIsMapScreen: true,
            hasUnrelatedLiveEncounter: true));
    }

    [Fact]
    public void CanOpenMapConversation_LiveEncounterWithSessionPartner_IsAllowed()
    {
        // The in-army/outsider initiator legitimately reaches this point still in an army_encounter with the
        // session's other party - that is not "unrelated" and must still open.
        Assert.True(PlayerPartyInteractionHandler.CanOpenMapConversation(
            atMenu: true,
            currentMenuId: "army_encounter",
            topScreenIsMapScreen: true,
            hasUnrelatedLiveEncounter: false));
    }

    [Fact]
    public void CanOpenMapConversation_UnrelatedEncounterOutranksBenignMenuAndMapScreen()
    {
        Assert.False(PlayerPartyInteractionHandler.CanOpenMapConversation(
            atMenu: false,
            currentMenuId: null,
            topScreenIsMapScreen: true,
            hasUnrelatedLiveEncounter: true));
    }

    [Fact]
    public void HasUnrelatedLiveEncounter_NoEncounter_IsFalse()
    {
        WithCampaign(playerEncounter: null, encounteredParty: null, () =>
        {
            var sessionOtherParty = ObjectHelper.SkipConstructor<PartyBase>();
            Assert.False(PlayerPartyInteractionHandler.HasUnrelatedLiveEncounter(
                sessionOtherParty, localPlayerInitiated: false));
        });
    }

    [Fact]
    public void HasUnrelatedLiveEncounter_ResponderWithEncounterAgainstAThirdParty_IsTrue()
    {
        var thirdParty = ObjectHelper.SkipConstructor<PartyBase>();
        var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();

        WithCampaign(encounter, thirdParty, () =>
        {
            var sessionOtherParty = ObjectHelper.SkipConstructor<PartyBase>();
            Assert.True(PlayerPartyInteractionHandler.HasUnrelatedLiveEncounter(
                sessionOtherParty, localPlayerInitiated: false));
        });
    }

    [Fact]
    public void HasUnrelatedLiveEncounter_InitiatorWithEncounterAgainstAThirdParty_IsFalse()
    {
        var chosenMember = ObjectHelper.SkipConstructor<PartyBase>();
        var originallyEncounteredArmy = ObjectHelper.SkipConstructor<PartyBase>();
        var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();

        WithCampaign(encounter, originallyEncounteredArmy, () =>
        {
            Assert.False(PlayerPartyInteractionHandler.HasUnrelatedLiveEncounter(
                chosenMember, localPlayerInitiated: true));
        });
    }

    [Fact]
    public void HasUnrelatedLiveEncounter_ResponderWithEncounterAgainstTheSessionParty_IsFalse()
    {
        // Responder side, but the encounter it is already in IS with the session counterpart - not unrelated.
        var sessionOtherParty = ObjectHelper.SkipConstructor<PartyBase>();
        var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();

        WithCampaign(encounter, sessionOtherParty, () =>
        {
            Assert.False(PlayerPartyInteractionHandler.HasUnrelatedLiveEncounter(
                sessionOtherParty, localPlayerInitiated: false));
        });
    }

    private static void WithCampaign(
        PlayerEncounter playerEncounter, PartyBase encounteredParty, System.Action body)
    {
        var previousCampaign = Campaign.Current;
        var campaign = ObjectHelper.SkipConstructor<Campaign>();
        campaign.PlayerEncounter = playerEncounter;

        if (playerEncounter != null)
        {
            playerEncounter._encounteredParty = encounteredParty;
        }

        try
        {
            Campaign.Current = campaign;
            body();
        }
        finally
        {
            Campaign.Current = previousCampaign;
        }
    }
}
