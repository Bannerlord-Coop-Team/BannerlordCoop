using Common.Messaging;
using Common.Util;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Clans.Patches;
using GameInterface.Tests.Services.SiegeEvents;
using HarmonyLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.Clans;

/// <summary>
/// Tests for the change-leader prefixes in ClanPartiesVMPatches.
/// </summary>
[Collection(nameof(CampaignCurrentCollection))]
public class ClanPartiesVMPatchesTests
{
    [Fact]
    public void OnPartyLeaderChanged_RefreshReselectsPlayerParty_StillTargetsPopupParty()
    {
        // A refresh arriving while the change-leader popup is open moves CurrentSelectedParty onto the player's own party.
        // Confirm the disband used to publish a command aimed at the player instead of the party the popup was opened for.
        RunWithCampaign(campaign =>
        {
            var companionParty = CreateParty();

            var vm = ObjectHelper.SkipConstructor<ClanPartiesVM>();
            vm._currentSelectedParty = CreatePartyItem(companionParty);

            ClanPartiesVMPatches.OnShowChangeLeaderPopupPrefix(vm);

            vm._currentSelectedParty = CreatePartyItem(campaign.MainParty);

            // A null new leader is the disband branch of the popup.
            var published = CaptureLeaderChanges(() =>
                Assert.False(ClanPartiesVMPatches.OnPartyLeaderChangedPrefix(vm, null)));

            ClanPartyLeaderChanged message = Assert.Single(published);
            Assert.Same(companionParty, message.SelectedParty);
            Assert.Same(campaign.MainParty, message.MainParty);
            Assert.Null(message.NewLeader);
        });
    }

    [Fact]
    public void OnPartyLeaderChanged_WithoutPopupCapture_TargetsCurrentSelection()
    {
        RunWithCampaign(_ =>
        {
            var companionParty = CreateParty();

            var vm = ObjectHelper.SkipConstructor<ClanPartiesVM>();
            vm._currentSelectedParty = CreatePartyItem(companionParty);

            var published = CaptureLeaderChanges(() =>
                Assert.False(ClanPartiesVMPatches.OnPartyLeaderChangedPrefix(vm, null)));

            ClanPartyLeaderChanged message = Assert.Single(published);
            Assert.Same(companionParty, message.SelectedParty);
        });
    }

    [Fact]
    public void OnPartyLeaderChanged_AfterScreenClosed_DoesNotReuseStalePopupParty()
    {
        // The captured party is static, so a closed clan screen must not leave one behind
        // for the next popup to publish against.
        RunWithCampaign(_ =>
        {
            var closedScreenParty = CreateParty();
            var reopenedParty = CreateParty();

            var vm = ObjectHelper.SkipConstructor<ClanPartiesVM>();
            vm._currentSelectedParty = CreatePartyItem(closedScreenParty);

            ClanPartiesVMPatches.OnShowChangeLeaderPopupPrefix(vm);
            ClanPartiesVMPatches.OnFinalizePostfix();

            vm._currentSelectedParty = CreatePartyItem(reopenedParty);

            var published = CaptureLeaderChanges(() =>
                Assert.False(ClanPartiesVMPatches.OnPartyLeaderChangedPrefix(vm, null)));

            ClanPartyLeaderChanged message = Assert.Single(published);
            Assert.Same(reopenedParty, message.SelectedParty);
        });
    }

    private static void RunWithCampaign(Action<Campaign> test)
    {
        Campaign previousCampaign = Campaign.Current;
        Game previousGame = Game.Current;
        try
        {
            var mainHero = ObjectHelper.SkipConstructor<Hero>();
            var mainCharacter = ObjectHelper.SkipConstructor<CharacterObject>();
            mainCharacter._heroObject = mainHero;
            mainHero._characterObject = mainCharacter;

            var game = ObjectHelper.SkipConstructor<Game>();
            game.PlayerTroop = mainCharacter;
            Game.Current = game;

            var campaign = ObjectHelper.SkipConstructor<Campaign>();
            campaign.MainParty = CreateParty();
            Campaign.Current = campaign;

            // The captured popup party is static; make sure no other test left one behind.
            ClanPartiesVMPatches.OnFinalizePostfix();

            test(campaign);
        }
        finally
        {
            ClanPartiesVMPatches.OnFinalizePostfix();
            Campaign.Current = previousCampaign;
            Game.Current = previousGame;
        }
    }

    private static List<ClanPartyLeaderChanged> CaptureLeaderChanges(Action act)
    {
        var published = new List<ClanPartyLeaderChanged>();
        Action<MessagePayload<ClanPartyLeaderChanged>> capture = payload => published.Add(payload.What);
        MessageBroker.Instance.Subscribe(capture);
        try
        {
            act();
        }
        finally
        {
            MessageBroker.Instance.Unsubscribe(capture);
        }
        return published;
    }

    private static MobileParty CreateParty()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var partyBase = ObjectHelper.SkipConstructor<PartyBase>();
        partyBase.MobileParty = party;
        AccessTools.Field(typeof(MobileParty), "<Party>k__BackingField").SetValue(party, partyBase);
        return party;
    }

    private static ClanPartyItemVM CreatePartyItem(MobileParty party)
    {
        var item = ObjectHelper.SkipConstructor<ClanPartyItemVM>();
        AccessTools.Field(typeof(ClanPartyItemVM), "<Party>k__BackingField").SetValue(item, party.Party);
        return item;
    }
}
