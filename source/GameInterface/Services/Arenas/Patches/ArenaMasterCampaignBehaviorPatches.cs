using Common;
using Common.Messaging;
using GameInterface.Services.Arenas.Messages;
using GameInterface.Services.Tournaments.UI;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Arenas.Patches;

[HarmonyPatch(typeof(ArenaMasterCampaignBehavior))]
internal class DisableArenaMasterCampaignBehavior
{
    [HarmonyPatch(nameof(ArenaMasterCampaignBehavior.OnGameLoadFinished))]
    [HarmonyPrefix]
    public static bool OnGameLoadFinishedPrefix() => ModInformation.IsClient;

    private static readonly bool ArenasDisabled = true;

    [HarmonyPatch(nameof(ArenaMasterCampaignBehavior.game_menu_enter_practice_fight_on_condition))]
    [HarmonyPrefix]
    public static bool GameMenuEnterPracticeFightOnConditionPrefix(ref bool __result, MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.PracticeFight;
        if (ArenasDisabled)
        {
            args.Tooltip = new TextObject("COMING SOON");
            args.IsEnabled = false;
            __result = true;
            return false;
        }

        return true;
    }

    [HarmonyPatch(nameof(ArenaMasterCampaignBehavior.conversation_arena_join_tournament_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationArenaJoinTournamentOnConsequencePrefix()
    {
        var menuId = CoopTournamentCampaignBehavior.EnterTournamentAndGetMenuId();

        Mission.Current.EndMission();
        Campaign.Current.GameMenuManager.SetNextMenu(menuId);

        return false;
    }

    private const string ArenaMasterAskToFight = "arena_master_ask_for_practice_fight_fight";

    [HarmonyPatch(nameof(ArenaMasterCampaignBehavior.AddDialogs))]
    [HarmonyPostfix]
    public static void AddDialogsPostfix()
    {
        var targetSentence = Campaign.Current.ConversationManager._sentences.FirstOrDefault(sentence => sentence.Id == ArenaMasterAskToFight);

        // Don't assign clickable condition if the sentence isn't found
        if (targetSentence == null) return;

        targetSentence.OnClickableCondition = ArenaPracticeFightsEnabled;
    }

    private static bool ArenaPracticeFightsEnabled(out TextObject hint)
    {
        hint = new TextObject("");
        if (ArenasDisabled)
        {
            hint = new TextObject("COMING SOON");
            return false;
        }

        return true;
    }

    [HarmonyPatch(nameof(ArenaMasterCampaignBehavior.conversation_arena_master_practice_fights_meet_on_condition))]
    [HarmonyPrefix]
    public static bool ConversationArenaMasterPracticeFightsMeetOnConditionPrefix(ArenaMasterCampaignBehavior __instance, ref bool __result)
    {
        if (CharacterObject.OneToOneConversationCharacter.Occupation == Occupation.ArenaMaster && !__instance._knowTournaments)
        {
            MBTextManager.SetTextVariable("TOWN_NAME", Settlement.CurrentSettlement.Name, false);
            __instance._knowTournaments = true;
            __instance._arenaMasterHasMetInSettlements.Add(Settlement.CurrentSettlement);

            // Update in server's CoopSession to persist across sessions
            var message = new AddMetArenaMasterAndKnowTournaments(Hero.MainHero, Settlement.CurrentSettlement);
            MessageBroker.Instance.Publish(__instance, message);

            __result = true;
            return false;
        }
        __result = false;
        return false;
    }

    [HarmonyPatch(nameof(ArenaMasterCampaignBehavior.conversation_arena_master_tournament_meet_on_condition))]
    [HarmonyPrefix]
    public static bool ConversationArenaMasterTournmentMeetOnConditionPrefix(ArenaMasterCampaignBehavior __instance, ref bool __result)
    {
        if (Settlement.CurrentSettlement == null)
        {
            __result = false;
            return false;
        }
        TournamentGame tournamentGame = Campaign.Current.TournamentManager.GetTournamentGame(Settlement.CurrentSettlement.Town);
        if (CharacterObject.OneToOneConversationCharacter.Occupation == Occupation.ArenaMaster && !__instance._knowTournaments && tournamentGame != null)
        {
            MBTextManager.SetTextVariable("TOWN_NAME", Settlement.CurrentSettlement.Name, false);
            __instance._knowTournaments = true;
            __instance._arenaMasterHasMetInSettlements.Add(Settlement.CurrentSettlement);

            // Update in server's CoopSession to persist across sessions
            var message = new AddMetArenaMasterAndKnowTournaments(Hero.MainHero, Settlement.CurrentSettlement);
            MessageBroker.Instance.Publish(__instance, message);

            __result = true;
            return false;
        }
        __result = false;
        return false;
    }

    [HarmonyPatch(nameof(ArenaMasterCampaignBehavior.conversation_arena_master_no_tournament_meet_on_condition))]
    [HarmonyPrefix]
    public static bool ConversationArenaMasterNoTournamentMeetOnConditionPrefix(ArenaMasterCampaignBehavior __instance, ref bool __result)
    {
        if (CharacterObject.OneToOneConversationCharacter.Occupation == Occupation.ArenaMaster && !__instance._knowTournaments)
        {
            MBTextManager.SetTextVariable("TOWN_NAME", Settlement.CurrentSettlement.Name, false);
            __instance._knowTournaments = true;
            __instance._arenaMasterHasMetInSettlements.Add(Settlement.CurrentSettlement);

            // Update in server's CoopSession to persist across sessions
            var message = new AddMetArenaMasterAndKnowTournaments(Hero.MainHero, Settlement.CurrentSettlement);
            MessageBroker.Instance.Publish(__instance, message);

            __result = true;
            return false;
        }
        __result = false;
        return false;
    }

    [HarmonyPatch(nameof(ArenaMasterCampaignBehavior.conversation_arena_master_meet_on_condition))]
    [HarmonyPrefix]
    public static bool ConversationArenaMasterMeetOnConditionPrefix(ArenaMasterCampaignBehavior __instance, ref bool __result)
    {
        if (CharacterObject.OneToOneConversationCharacter.Occupation == Occupation.ArenaMaster && __instance._knowTournaments && Settlement.CurrentSettlement.IsTown && !__instance._arenaMasterHasMetInSettlements.Contains(Settlement.CurrentSettlement))
        {
            MBTextManager.SetTextVariable("TOWN_NAME", Settlement.CurrentSettlement.Name, false);
            __instance._arenaMasterHasMetInSettlements.Add(Settlement.CurrentSettlement);

            // Update in server's CoopSession to persist across sessions
            var message = new AddMetArenaMaster(Hero.MainHero, Settlement.CurrentSettlement);
            MessageBroker.Instance.Publish(__instance, message);

            __result = true;
            return false;
        }
        __result = false;
        return false;
    }

    [HarmonyPatch(nameof(ArenaMasterCampaignBehavior.game_menu_enter_practice_fight_on_consequence))]
    [HarmonyPrefix]
    public static bool GameMenuEnterPracticeFightOnConsequencePrefix(ArenaMasterCampaignBehavior __instance, MenuCallbackArgs args)
    {
        if (!__instance._arenaMasterHasMetInSettlements.Contains(Settlement.CurrentSettlement))
        {
            __instance._arenaMasterHasMetInSettlements.Add(Settlement.CurrentSettlement);

            // Update in server's CoopSession to persist across sessions
            var message = new AddMetArenaMaster(Hero.MainHero, Settlement.CurrentSettlement);
            MessageBroker.Instance.Publish(__instance, message);
        }

        PlayerEncounter.LocationEncounter.CreateAndOpenMissionController(LocationComplex.Current.GetLocationWithId("arena"), null, null, null);
        __instance._enteredPracticeFightFromMenu = true;

        return false;
    }
}
