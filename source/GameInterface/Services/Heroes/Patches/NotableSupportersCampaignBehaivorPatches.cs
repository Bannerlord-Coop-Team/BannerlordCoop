using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Heroes.Patches;

// Don't disable this campaign behavior for clients
// It is entirely made up of methods called by client interactions in dialogue
[HarmonyPatch(typeof(NotableSupportersCampaignBehavior))]
internal class NotableSupportersCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(NotableSupportersCampaignBehavior.notable_support_player_decision_accept_on_consequences))]
    [HarmonyPrefix]
    public static bool NotableSupportPlayerDecisionAcceptOnConsequences(ref NotableSupportersCampaignBehavior __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Manage all changed properties on server
        int cost = Campaign.Current.Models.NotablePowerModel.GetInitialNotableSupporterCost(Hero.OneToOneConversationHero);
        var message = new NotableSupportAccepted(Hero.MainHero, Hero.OneToOneConversationHero, Clan.PlayerClan, cost);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(NotableSupportersCampaignBehavior.notable_support_end_agreement_on_consequences))]
    [HarmonyPrefix]
    public static bool NotableSupportEndAgreementOnConsequencesPrefix(ref NotableSupportersCampaignBehavior __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Manage changed OneToOneConversationHero.SupporterOf on server
        var message = new NotableSupportEndedByAgreement(Hero.OneToOneConversationHero);
        MessageBroker.Instance.Publish(__instance, message);

        // Need to display text 
        TextObject textObject = new TextObject("{=afzeDAPd}{NOTABLE.NAME} no longer supports your clan.", null);
        textObject.SetCharacterProperties("NOTABLE", Hero.OneToOneConversationHero.CharacterObject, false);
        InformationManager.DisplayMessage(new InformationMessage(textObject.ToString(), new Color(0f, 1f, 0f, 1f)));
        
        return false;
    }
}
