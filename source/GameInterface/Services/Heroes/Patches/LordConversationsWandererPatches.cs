using HarmonyLib;
using Helpers;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(LordConversationsCampaignBehavior))]
internal class LordConversationsWandererPatches
{
    private const string AlleyDialogueId = "wanderer_job_status_1";

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.AddWandererConversations))]
    [HarmonyPostfix]
    public static void AddWandererConversationsPostfix()
    {
        var targetSentence = Campaign.Current.ConversationManager._sentences.FirstOrDefault(sentence => sentence.Id == AlleyDialogueId);

        if (targetSentence == null) return;

        // Vanilla incorrectly uses the NPC's gender to address the player
        targetSentence.Text = new TextObject("{=EUBxMVXk}Do you have any orders for your alley, {?PLAYER.GENDER}madam{?}sir{\\?}");
    }

    [ThreadStatic]
    private static bool UseWandererIntroduction = false;

    /// <summary>
    /// Use wanderer dialogue introduction regardless of recruited status.
    /// After introduction, treat dialogue as a lord conversation by ConversationWandererOnConditionPrefix.
    /// </summary>
    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_wanderer_meet_on_condition))]
    [HarmonyPrefix]
    public static bool ConversationWandererMeetOnConditionPrefix(LordConversationsCampaignBehavior __instance, ref bool __result)
    {
        __result = IsNonPrisonerWanderer(CharacterObject.OneToOneConversationCharacter)
            && __instance.ConversationUseMeetingDialogs();

        if (__result)
        {
            UseWandererIntroduction = true;
        }

        return false;
    }

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_wanderer_meet_player_on_condition))]
    [HarmonyPrefix]
    public static bool ConversationWandererMeetPlayerOnConditionPrefix(ref bool __result)
    {
        __result = UseWandererIntroduction;
        return false;
    }

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_wanderer_generic_introduction_on_condition))]
    [HarmonyPrefix]
    public static bool ConversationWandererGenericIntroductionOnConditionPostfix(ref bool __result)
    {
        if (UseWandererIntroduction)
        {
            __result = true;
            StringHelpers.SetCharacterProperties("CONVERSATION_CHARACTER", Hero.OneToOneConversationHero.CharacterObject, null, false);
        }
        return false;
    }

    /// <summary>
    /// Override result to check if in the same clan rather than just checking if a player companion
    /// </summary>
    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_wanderer_player_owned_on_condition))]
    [HarmonyPrefix]
    public static bool ConversationWandererPlayerOwnedOnConditionPrefix(ref bool __result)
    {
        __result = CharacterObject.OneToOneConversationCharacter != null
            && CharacterObject.OneToOneConversationCharacter.IsHero
            && Hero.OneToOneConversationHero.CompanionOf != null
            && Hero.OneToOneConversationHero.Clan == Clan.PlayerClan;

        return false;
    }

    /// <summary>
    /// Don't load wanderer dialogue for companions of a different clan, instead treat the dialogue as a lord dialogue.
    /// All companion dialogue in vanilla assumes they are a companion of the player in the conversation.
    /// </summary>
    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_wanderer_on_condition))]
    [HarmonyPrefix]
    public static bool ConversationWandererOnConditionPrefix(ref bool __result)
    {
        __result = UseWandererIntroduction || IsNonPrisonerWanderer(CharacterObject.OneToOneConversationCharacter)
            && (Hero.OneToOneConversationHero.CompanionOf == null
            || Hero.OneToOneConversationHero.CompanionOf == Clan.PlayerClan);

        UseWandererIntroduction = false;

        return false;
    }

    private static bool IsNonPrisonerWanderer(CharacterObject character)
    {
        return character != null
            && character.IsHero
            && character.Occupation == Occupation.Wanderer
            && character.HeroObject.HeroState != Hero.CharacterStates.Prisoner;
    }

    /// <summary>
    /// Override result for companions of a different clan to the player
    /// This allows players to attack wanderers parties (caravan & lord parties) of other player clans
    /// </summary>
    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_lord_is_threated_neutral_on_condition))]
    [HarmonyPrefix]
    public static bool ConversationLordIsTreatedNeutralOnConditionPrefix(ref bool __result)
    {
        __result = CharacterObject.OneToOneConversationCharacter.IsHero
            && !CharacterObject.OneToOneConversationCharacter.HeroObject.IsPrisoner
            && Campaign.Current.CurrentConversationContext == ConversationContext.PartyEncounter
            && Settlement.CurrentSettlement == null
            && CharacterObject.OneToOneConversationCharacter.HeroObject.CompanionOf != Clan.PlayerClan
            && FactionManager.IsNeutralWithFaction(Hero.OneToOneConversationHero.MapFaction, Hero.MainHero.MapFaction)
            && !MobileParty.MainParty.IsInRaftState;

        return false;
    }
}
