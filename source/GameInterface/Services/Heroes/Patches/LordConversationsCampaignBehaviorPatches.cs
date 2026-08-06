using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages.LordConversations;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(LordConversationsCampaignBehavior))]
internal class LordConversationsCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.OnBarterAccepted))]
    [HarmonyPrefix]
    public static bool OnBarterAcceptedPrefix()
    {
        return !ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_player_liberates_prisoner_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationPlayerLiberatesPrisonerOnConsequencePrefix()
    {
        var message = new LiberateLordPrisoner(Hero.MainHero, Hero.OneToOneConversationHero);
        MessageBroker.Instance.Publish(null, message);
        MarkFreedLordConversationHandled(Hero.OneToOneConversationHero);

        return false;
    }

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_player_fails_to_release_prisoner_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationPlayerFailsToReleasePrisonerOnConsequencePrefix()
    {
        if (!Hero.OneToOneConversationHero.IsPrisoner) return false;

        var message = new TakeLordPrisoner(Campaign.Current.MainParty.Party, Hero.OneToOneConversationHero);
        MessageBroker.Instance.Publish(null, message);
        MarkFreedLordConversationHandled(Hero.OneToOneConversationHero);

        return false;
    }

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_ally_thanks_meet_after_helping_in_battle_2_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationAllyThanksMeetAfterHelpingInBattle2OnConsequencePrefix()
    {
        // TODO: PlayerMapEvent will be null here. Need to get the relation change without it
        //int playerGainedRelationAmount = Campaign.Current.Models.BattleRewardModel.GetPlayerGainedRelationAmount(MapEvent.PlayerMapEvent, Hero.OneToOneConversationHero);

        var message = new LordHelpedInBattle(Hero.MainHero, Hero.OneToOneConversationHero); // playerGainedRelationAmount
        MessageBroker.Instance.Publish(null, message);

        return false;
    }

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_talk_lord_defeat_to_lord_capture_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationTalkLordDefeatToLordCaptureOnConsequencePrefix()
    {
        Campaign.Current.CurrentConversationContext = ConversationContext.Default;

        var message = new TakeLordPrisoner(Campaign.Current.MainParty.Party, CharacterObject.OneToOneConversationCharacter.HeroObject);
        MessageBroker.Instance.Publish(null, message);

        return false;
    }

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_talk_lord_defeat_to_lord_release_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationTalkLordDefeatToLordReleaseOnConsequencePrefix()
    {
        DialogHelper.SetDialogString("DEFEAT_LORD_ANSWER", "str_prisoner_released");

        var message = new LordDefeatToRelease(Hero.MainHero, Hero.OneToOneConversationHero);
        MessageBroker.Instance.Publish(null, message);

        return false;
    }

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_talk_lord_freed_to_lord_capture_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationTalkLordFreedToLordCaptureOnConsequencePrefix()
    {
        Campaign.Current.CurrentConversationContext = ConversationContext.Default;

        var message = new TakeLordPrisoner(Campaign.Current.MainParty.Party, Hero.OneToOneConversationHero);
        MessageBroker.Instance.Publish(null, message);
        MarkFreedLordConversationHandled(Hero.OneToOneConversationHero);

        return false;
    }

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_talk_lord_freed_to_lord_release_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationTalkLordFreedToLordReleaseOnConsequencePrefix()
    {
        var message = new LordFreedToRelease(Hero.MainHero, Hero.OneToOneConversationHero);
        MessageBroker.Instance.Publish(null, message);
        MarkFreedLordConversationHandled(Hero.OneToOneConversationHero);

        return false;
    }

    private static void MarkFreedLordConversationHandled(Hero conversationHero)
    {
        var pendingHeroes = PlayerEncounter.Current?._capturedAlreadyPrisonerHeroes;
        if (pendingHeroes == null) return;

        // Vanilla advances this list through its synchronous captivity action before the next encounter update.
        pendingHeroes.RemoveAll(element => element.Character?.HeroObject == conversationHero);
    }
}
