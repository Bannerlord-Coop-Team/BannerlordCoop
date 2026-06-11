using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Companions.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Companions.Patches;

[HarmonyPatch(typeof(CompanionRolesCampaignBehavior))]
internal class CompanionRolesPatches
{
    private static readonly ILogger logger = LogManager.GetLogger<CompanionRolesPatches>();

    [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.ClanNameSelectionIsDone))]
    [HarmonyPrefix]
    public static bool ClanNameSelectionIsDonePrefix(ref CompanionRolesCampaignBehavior __instance, string clanName)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        var message = new ClanNameSelectionDone(
            Hero.MainHero,
            Hero.OneToOneConversationHero,
            __instance.CurrentBehavior._selectedFief,
            MobileParty.MainParty,
            clanName
        );
        MessageBroker.Instance.Publish(__instance, message);

        Campaign.Current.ConversationManager.ContinueConversation();

        return false;
    }

    [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.companion_fire_on_consequence))]
    [HarmonyPrefix]
    public static bool CompanionFireOnConsequencePrefix(ref CompanionRolesCampaignBehavior __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        var message = new CompanionFired(Hero.OneToOneConversationHero);
        MessageBroker.Instance.Publish(__instance, message);

        if (PlayerEncounter.Current != null)
        {
            PlayerEncounter.LeaveEncounter = true;
        }

        return false;
    }

    [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.companion_rejoin_after_emprisonment_role_on_consequence))]
    [HarmonyPrefix]
    public static bool CompanionRejoinAfterEmprisonmentRoleOnConsequencePrefix(ref CompanionRolesCampaignBehavior __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        var message = new CompanionRejoinAfterEmprisonment(
            Hero.OneToOneConversationHero,
            MobileParty.MainParty);
        MessageBroker.Instance.Publish(__instance, message);

        // AddHeroToPartyAction already blocked on client, need to update the ConversationManager on the client 
        return true;
    }

    [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.companion_rescue_answer_options_join_party_consequence))]
    [HarmonyPrefix]
    public static bool CompanionRescueAnswerOptionsJoinPartyConsequencePrefix(ref CompanionRolesCampaignBehavior __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        var message = new CompanionJoinedPartyByRescue(
            Hero.OneToOneConversationHero,
            MobileParty.MainParty);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.PartyScreenClosed))]
    [HarmonyPrefix]
    public static bool PartyScreenClosedPrefix(ref CompanionRolesCampaignBehavior __instance, PartyBase leftOwnerParty, TroopRoster leftMemberRoster, TroopRoster leftPrisonRoster, PartyBase rightOwnerParty, TroopRoster rightMemberRoster, TroopRoster rightPrisonRoster, bool fromCancel)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (fromCancel) return false;

        var message = new PartyScreenClosedFromRescuing(
            leftOwnerParty,
            leftMemberRoster,
            leftPrisonRoster,
            rightOwnerParty,
            rightMemberRoster,
            rightPrisonRoster);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.end_rescue_companion))]
    [HarmonyPrefix]
    public static bool EndRescueCompanionPrefix(ref CompanionRolesCampaignBehavior __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        __instance._partyCreatedAfterRescueForCompanion = false;
        if (Hero.OneToOneConversationHero.IsPrisoner)
        {
            var message = new CompanionRescued(Hero.OneToOneConversationHero);
            MessageBroker.Instance.Publish(__instance, message);
        }

        return false;
    }
}
