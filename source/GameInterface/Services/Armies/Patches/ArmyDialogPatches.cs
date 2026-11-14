using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Armies.Patches;

/// <summary>
/// Routes the player's army join and leave
/// choices from encounter menus and lord
/// conversations to the server.
/// </summary>
[HarmonyPatch]
internal class ArmyDialogPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<ArmyDialogPatches>();

    [HarmonyPatch(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.game_menu_army_join_on_consequence))]
    [HarmonyPrefix]
    private static bool Prefix(EncounterGameMenuBehavior __instance, MenuCallbackArgs args)
    {
        var message = new MobilePartyInArmyAdded(PlayerEncounter.EncounteredMobileParty.Army, MobileParty.MainParty, addPartyToMergedPartiesBool: true);
        MessageBroker.Instance.Publish(__instance, message);
        PlayerEncounter.Finish(true);
        return false;
    }

    [HarmonyPatch(typeof(LordConversationsCampaignBehavior), nameof(LordConversationsCampaignBehavior.lord_considers_army_on_consequence))]
    [HarmonyPrefix]
    private static bool Prefixlord_considers_army_on_consequence(LordConversationsCampaignBehavior __instance)
    {
        var army = Hero.OneToOneConversationHero.PartyBelongedTo.Army;
        var message = new MobilePartyInArmyAdded(Hero.OneToOneConversationHero.PartyBelongedTo.Army, MobileParty.MainParty, addPartyToMergedPartiesBool: true);
        MessageBroker.Instance.Publish(__instance, message);
        if (!GameThread.WaitWhilePumping(() => MobileParty.MainParty.Army == army, DateTime.UtcNow.AddSeconds(5)))
        {
            Logger.Error("Player could not join army {Army}", army.Name);
            return false;
        }

        if (PlayerEncounter.InsideSettlement)
            GameMenu.SwitchToMenu("army_wait_at_settlement");
        return false;
    }

    [HarmonyPatch(typeof(LordConversationsCampaignBehavior), nameof(LordConversationsCampaignBehavior.conversation_lord_tell_gathering_player_joined_on_consequence))]
    [HarmonyPrefix]
    private static bool Prefixconversation_lord_tell_gathering_player_joined_on_consequence(LordConversationsCampaignBehavior __instance)
    {
        var message = new MobilePartyInArmyAdded(Hero.OneToOneConversationHero.PartyBelongedTo.Army, MobileParty.MainParty, addPartyToMergedPartiesBool: false);
        MessageBroker.Instance.Publish(__instance, message);
        return false;
    }

    [HarmonyPatch(typeof(LordConversationsCampaignBehavior), nameof(LordConversationsCampaignBehavior.lord_considers_joining_player_army_on_consequence))]
    [HarmonyPrefix]
    private static bool Prefixlord_considers_joining_player_army_on_consequence(LordConversationsCampaignBehavior __instance)
    {
        var party = Hero.OneToOneConversationHero.PartyBelongedTo;
        var message = new MobilePartyInArmyAdded(MobileParty.MainParty.Army, party, addPartyToMergedPartiesBool: true);
        MessageBroker.Instance.Publish(__instance, message);
        return false;
    }

    [HarmonyPatch(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.game_menu_encounter_abandon_on_consequence))]
    [HarmonyPrefix]
    private static bool Prefixgame_menu_encounter_abandon_on_consequence(EncounterGameMenuBehavior __instance)
    {
        var mainParty = MobileParty.MainParty;
        ((PlayerEncounter.Battle != null) ? PlayerEncounter.Battle : PlayerEncounter.EncounteredBattle).BeginWait();
        mainParty.SetMoveModeHold();
        var message = new MobilePartyInArmyRemoved(mainParty.Army, mainParty, mainParty, abandonArmy: false);
        MessageBroker.Instance.Publish(__instance, message);
        PlayerEncounter.Finish(true);
        if (mainParty.BesiegerCamp != null)
        {
            MessageBroker.Instance.Publish(
                null,
                new BreakSiegeAttempted(mainParty, finishLocalMenus: false));
        }
        return false;
    }

    [HarmonyPatch(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.leave_army_after_attack_on_consequence))]
    [HarmonyPrefix]
    private static bool Prefixleave_army_after_attack_on_consequence(EncounterGameMenuBehavior __instance)
    {
        if (PlayerEncounter.Current != null)
        {
            PlayerEncounter.Finish(true);
        }
        else
        {
            GameMenu.ExitToLast();
        }
        if (Settlement.CurrentSettlement != null)
        {
            MessageBroker.Instance.Publish(MobileParty.MainParty, new EndSettlementEncounterAttempted(MobileParty.MainParty));
            PartyBase.MainParty.SetVisualAsDirty();
        }
        var message = new MobilePartyInArmyRemoved(MobileParty.MainParty.Army, MobileParty.MainParty, MobileParty.MainParty, abandonArmy: false);
        MessageBroker.Instance.Publish(__instance, message);
        return false;
    }
}
