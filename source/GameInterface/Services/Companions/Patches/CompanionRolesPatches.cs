using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Companions.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Companions.Patches;

[HarmonyPatch(typeof(CompanionRolesCampaignBehavior))]
internal class CompanionRolesPatches
{
    private static readonly ILogger logger = LogManager.GetLogger<CompanionRolesPatches>();

    // HeroRelationChanged (should only have to disable ChangeRelationAction.ApplyInternal on client)
    // OnCompanionRemoved (disable RemoveCompanionAction.ApplyInternal on client)
    // ClanNameSelectionIsDone
    // - AdjustCompanionsEquipment
    // - SpawnNewHeroesForNewCompanionClan
    // companion_fire_on_consequence
    // companion_rejoin_after_emprisonment_role_on_consequence
    // companion_rescue_answer_options_join_party_consequence
    // PartyScreenClosed
    // end_rescue_companion

    // Managed in MobileParty?
    // companion_becomes_engineer_on_consequence
    // companion_becomes_surgeon_on_consequence
    // companion_becomes_quartermaster_on_consequence
    // companion_becomes_scout_on_consequence
    // companion_fire_engineer_on_consequence
    // companion_fire_surgeon_on_consequence
    // companion_fire_quartermaster_on_consequence
    // companion_fire_scout_on_consequence

    [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.companion_fire_on_consequence))]
    [HarmonyPrefix]
    public static bool CompanionFireOnConsequencePrefix(ref CompanionRolesCampaignBehavior __instance)
    {
        /*
        var message = new CompanionBecomesEngineer(
            Hero.MainHero,
            Hero.OneToOneConversationHero,
            Campaign.Current.Models.CompanionHiringPriceCalculationModel.GetCompanionHiringPrice(Hero.OneToOneConversationHero),
            Clan.PlayerClan,
            MobileParty.MainParty
        );
        MessageBroker.Instance.Publish(__instance, message);
        */

        return false;
    }
}
