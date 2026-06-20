using Common.Messaging;
using GameInterface.Services.Caravans.Messages;
using HarmonyLib;
using Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Caravans.Patches;

[HarmonyPatch(typeof(CaravanConversationsCampaignBehavior))]
internal class CaravanConversationsCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(CaravanConversationsCampaignBehavior.conversation_magistrate_form_a_caravan_accept_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationMagistrateFormACaravanAcceptOnConsequence(ref CaravanConversationsCampaignBehavior __instance)
    {
        CharacterObject characterObject = ConversationSentence.SelectedRepeatObject as CharacterObject;

        // Run on server? Send to other clients?
        __instance.FadeOutSelectedCaravanCompanionInMission(characterObject);
        
        bool isElite = __instance._selectedCaravanType == 1;
        bool shouldCreateConvoy = __instance.ShouldCreateConvoy(); // Used by warsails
        int goldCost = (!isElite) ? __instance.GetSmallCaravanGoldCost() : __instance.GetLargeCaravanGoldCost();

        var message = new PlayerClanCaravanFormed(Hero.MainHero, characterObject.HeroObject, Settlement.CurrentSettlement, isElite, shouldCreateConvoy, goldCost);
        MessageBroker.Instance.Publish(__instance, message);

        // Notify client of formed caravan
        TextObject textObject;
        if (shouldCreateConvoy) // Warsails related
        {
            textObject = new TextObject("{=c7VOPmSb}A new trade convoy is created for {HERO.NAME}.", null);
        }
        else
        {
            textObject = new TextObject("{=RmtTsqcx}A new caravan is created for {HERO.NAME}.", null);
        }
        StringHelpers.SetCharacterProperties("HERO", Hero.MainHero.CharacterObject, textObject, false);
        InformationManager.DisplayMessage(new InformationMessage(textObject.ToString()));

        return false;
    }
}
