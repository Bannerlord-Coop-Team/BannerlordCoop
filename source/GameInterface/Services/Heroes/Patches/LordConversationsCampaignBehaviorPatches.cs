using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages.LordConversations;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(LordConversationsCampaignBehavior))]
internal class LordConversationsCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_player_liberates_prisoner_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationPlayerLiberatesPrisonerOnConsequencePrefix(
        LordConversationsCampaignBehavior __instance)
    {
        var message = new LiberateLordPrisoner(Hero.MainHero, Hero.OneToOneConversationHero);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_player_fails_to_release_prisoner_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationPlayerFailsToReleasePrisonerOnConsequencePrefix(
        LordConversationsCampaignBehavior __instance)
    {
        if (!Hero.OneToOneConversationHero.IsPrisoner) return false;

        var message = new TakeLordPrisoner(Campaign.Current.MainParty.Party, Hero.OneToOneConversationHero);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_ally_thanks_meet_after_helping_in_battle_2_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationAllyThanksMeetAfterHelpingInBattle2OnConsequencePrefix(
        LordConversationsCampaignBehavior __instance)
    {
        // TODO: PlayerMapEvent will be null here. Need to get the relation change without it
        //int playerGainedRelationAmount = Campaign.Current.Models.BattleRewardModel.GetPlayerGainedRelationAmount(MapEvent.PlayerMapEvent, Hero.OneToOneConversationHero);

        var message = new LordHelpedInBattle(Hero.MainHero, Hero.OneToOneConversationHero); // playerGainedRelationAmount
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_talk_lord_defeat_to_lord_capture_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationTalkLordDefeatToLordCaptureOnConsequencePrefix(
        LordConversationsCampaignBehavior __instance)
    {
        Campaign.Current.CurrentConversationContext = ConversationContext.Default;

        var message = new TakeLordPrisoner(Campaign.Current.MainParty.Party, CharacterObject.OneToOneConversationCharacter.HeroObject);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_talk_lord_defeat_to_lord_release_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationTalkLordDefeatToLordReleaseOnConsequencePrefix(
        LordConversationsCampaignBehavior __instance)
    {
        DialogHelper.SetDialogString("DEFEAT_LORD_ANSWER", "str_prisoner_released");

        var message = new LordDefeatToRelease(Hero.MainHero, Hero.OneToOneConversationHero);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_talk_lord_freed_to_lord_capture_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationTalkLordFreedToLordCaptureOnConsequencePrefix(
        LordConversationsCampaignBehavior __instance)
    {
        Campaign.Current.CurrentConversationContext = ConversationContext.Default;

        var message = new TakeLordPrisoner(Campaign.Current.MainParty.Party, Hero.OneToOneConversationHero);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_talk_lord_freed_to_lord_release_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationTalkLordFreedToLordReleaseOnConsequencePrefix(
        LordConversationsCampaignBehavior __instance)
    {
        var message = new LordFreedToRelease(Hero.MainHero, Hero.OneToOneConversationHero);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    // ????? conversation_talk_lord_defeat_to_lord_capture_and_kill_on_consequence
}
