using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(ClanPartiesVM))]
internal class ClanPartiesVMPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClanPartiesVMPatches>();

    private const string PlayerHeroRejectedMessage = "Failed to change clan party leader because hero is a player.";

    [HarmonyPatch(nameof(ClanPartiesVM.GetCanCreateNewParty))]
    [HarmonyPrefix]
    public static bool GetCanCreateNewPartyPrefix(ClanPartiesVM __instance, ref bool __result, ref TextObject disabledReason)
    {
        if (CoopClanPermissions.CanManageClan(__instance._faction)) return true;
        __result = false;
        disabledReason = GameTexts.FindText("str_coop_clan_party_leader_only");
        return false;
    }

    [HarmonyPatch(nameof(ClanPartiesVM.OnShowNewPartyPopup))]
    [HarmonyPrefix]
    public static bool NewPartyPopupPrefix(ClanPartiesVM __instance) => CoopClanPermissions.CanManageClan(__instance._faction);

    [HarmonyPatch(nameof(ClanPartiesVM.OnNewPartyCreationOver))]
    [HarmonyPrefix]
    public static bool NewPartyCreationOverPrefix(ClanPartiesVM __instance) => CoopClanPermissions.CanManageClan(__instance._faction);

    [HarmonyPatch(nameof(ClanPartiesVM.GetCanDisbandParty))]
    [HarmonyPrefix]
    public static bool GetCanDisbandPartyPrefix(ClanPartiesVM __instance, ref bool __result, ref TextObject cannotDisbandReason)
    {
        var party = __instance.CurrentSelectedParty?.Party?.MobileParty;
        if (CoopClanPermissions.CanManageParty(party)) return true;
        cannotDisbandReason = GameTexts.FindText(party?.IsPlayerParty() == true
            ? "str_coop_clan_player_party_protected" : "str_coop_clan_party_leader_only");
        __result = false;
        return false;
    }

    [HarmonyPatch(nameof(ClanPartiesVM.CreateNewClanParty))]
    [HarmonyPrefix]
    public static bool CreateNewClanPartyPrefix(ClanPartiesVM __instance, Hero newLeader, int partyGoldLowerThreshold)
    {
        if (!CoopClanPermissions.CanManageClan(__instance._faction) || newLeader == null) return false;

        // Reject forming a new party with a player hero
        if (newLeader.IsPlayerHero())
        {
            Logger.Error($"Rejecting new clan mobile party because newLeader is a player hero ({newLeader.StringId}).");

            // Inform client of rejected clan party leader change
            InformationManager.DisplayMessage(new InformationMessage(PlayerHeroRejectedMessage));
            return false;
        }

        if (!CoopClanPermissions.CanManageHero(newLeader)) return false;

        if (newLeader.PartyBelongedTo == MobileParty.MainParty)
        {
            __instance._openPartyAsManage(newLeader);
            __instance.RefreshPartiesList();
            return false;
        }

        // Create and manage the new mobile party on the server
        var message = new NewClanPartyCreated(Hero.MainHero, newLeader, __instance._faction, partyGoldLowerThreshold);
        MessageBroker.Instance.Publish(__instance, message);

        __instance._onRefresh();

        return false;
    }

    /// <summary>
    /// The party the change-leader popup was opened for. Save to use in OnPartyLeaderChangedPrefix.
    /// Any incoming refresh messages can change ClanPartiesVM.CurrentSelectedParty to the player's party.
    /// </summary>
    private static MobileParty popupParty;

    [HarmonyPatch(nameof(ClanPartiesVM.OnShowChangeLeaderPopup))]
    [HarmonyPrefix]
    public static bool OnShowChangeLeaderPopupPrefix(ClanPartiesVM __instance)
    {
        popupParty = null;
        var party = __instance.CurrentSelectedParty?.Party?.MobileParty;
        if (!CoopClanPermissions.CanManageParty(party)) return false;

        popupParty = party;
        return true;
    }

    [HarmonyPatch(nameof(ClanPartiesVM.OnPartyLeaderChanged))]
    [HarmonyPrefix]
    public static bool OnPartyLeaderChangedPrefix(ClanPartiesVM __instance, Hero newLeader)
    {
        // Use popupParty instead of the CurrentSelectedParty that can change from any incoming refresh messages
        var selectedParty = popupParty ?? __instance.CurrentSelectedParty?.Party?.MobileParty;
        popupParty = null;

        if (selectedParty == null) return false;
        if (!CoopClanPermissions.CanManageParty(selectedParty)) return false;

        var oldLeader = selectedParty.Party?.LeaderHero;
        if (oldLeader != null && oldLeader.IsPlayerHero())
        {
            Logger.Error($"Rejecting change of leader in clan mobile party because oldLeader is a player hero ({oldLeader.StringId}).");

            // Inform client of rejected clan party leader change
            InformationManager.DisplayMessage(new InformationMessage(PlayerHeroRejectedMessage));
            return false;
        }

        if (newLeader != null && newLeader.IsPlayerHero())
        {
            Logger.Error($"Rejecting change of leader in clan mobile party because newLeader is a player hero ({newLeader.StringId}).");

            // Inform client of rejected clan party leader change
            InformationManager.DisplayMessage(new InformationMessage(PlayerHeroRejectedMessage));
            return false;
        }

        // Change clan party leader on the server
        if (newLeader != null && !CoopClanPermissions.CanManageHero(newLeader)) return false;

        var message = new ClanPartyLeaderChanged(Hero.MainHero, newLeader, selectedParty, MobileParty.MainParty);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(ClanPartiesVM.OnDisbandCurrentParty))]
    [HarmonyPrefix]
    public static bool OnDisbandCurrentPartyPrefix()
    {
        // Block and implement as part of OnPartyLeaderChanged to use correct party
        // instead of currently selected (which can switch back to the player's party)
        return false;
    }

    [HarmonyPatch(nameof(ClanPartiesVM.OnFinalize))]
    [HarmonyPostfix]
    public static void OnFinalizePostfix()
    {
        popupParty = null;
    }

    [HarmonyPatch(nameof(ClanPartiesVM.GetNewPartyLeaderCandidates))]
    [HarmonyPostfix]
    public static void GetNewPartyLeaderCandidatesPostfix(ref IEnumerable<ClanCardSelectionItemInfo> __result)
    {
        __result = WithManageableHeroes(__result);
    }

    [HarmonyPatch(nameof(ClanPartiesVM.GetChangeLeaderCandidates))]
    [HarmonyPostfix]
    public static void GetChangeLeaderCandidatesPostfix(ref IEnumerable<ClanCardSelectionItemInfo> __result)
    {
        __result = WithManageableHeroes(__result);
    }

    private static IEnumerable<ClanCardSelectionItemInfo> WithManageableHeroes(IEnumerable<ClanCardSelectionItemInfo> candidates)
    {
        if (candidates == null) return candidates;

        return candidates
            .Where(candidate => !(candidate.Identifier is Hero hero) ||
                (!hero.IsPlayerHero() && CoopClanPermissions.CanManageHero(hero)))
            .ToList();
    }
}
