using Common.Messaging;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(PatrolPartiesCampaignBehavior))]
internal class PatrolPartiesInteractionsPatches
{
    [HarmonyPatch(nameof(PatrolPartiesCampaignBehavior.patrol_talk_on_consequence))]
    [HarmonyPostfix]
    public static void PatrolTalkOnConsequencePostfix(PatrolPartiesCampaignBehavior __instance)
    {
        // Save interaction in CoopSession
        var message = new AddPatrolPartyInteraction(Hero.MainHero, MobileParty.ConversationParty.HomeSettlement, CampaignTime.Now);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(PatrolPartiesCampaignBehavior.patrol_attack_on_consequence))]
    [HarmonyPrefix]
    public static bool PatrolAttackOnConsequencePrefix(PatrolPartiesCampaignBehavior __instance)
    {
        var message = new PatrolPartyHostileAction(PartyBase.MainParty, MobileParty.ConversationParty.Party);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }
}
