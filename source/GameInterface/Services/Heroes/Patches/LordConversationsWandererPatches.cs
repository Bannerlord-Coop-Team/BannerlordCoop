using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(LordConversationsCampaignBehavior))]
internal class LordConversationsWandererPatches
{
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
        __result = CharacterObject.OneToOneConversationCharacter != null
            && CharacterObject.OneToOneConversationCharacter.IsHero
            && CharacterObject.OneToOneConversationCharacter.Occupation == Occupation.Wanderer
            && CharacterObject.OneToOneConversationCharacter.HeroObject.HeroState != Hero.CharacterStates.Prisoner
            && Hero.OneToOneConversationHero.CompanionOf != null
            && Hero.OneToOneConversationHero.Clan == Clan.PlayerClan;

        return false;
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
            && CharacterObject.OneToOneConversationCharacter.HeroObject.CompanionOf != null
            && CharacterObject.OneToOneConversationCharacter.HeroObject.CompanionOf != Clan.PlayerClan
            && FactionManager.IsNeutralWithFaction(Hero.OneToOneConversationHero.MapFaction, Hero.MainHero.MapFaction)
            && !MobileParty.MainParty.IsInRaftState;

        return false;
    }
}
