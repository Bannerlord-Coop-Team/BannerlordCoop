using Common;
using Common.Messaging;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Library;

namespace GameInterface.Services.Clans.Patches;

/// <summary>
/// Routes the mercenary-service conversation consequence through the authoritative server.
/// </summary>
[HarmonyPatch(typeof(LordConversationsCampaignBehavior))]
internal class MercenaryServiceConversationPatch
{
    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_mercenary_player_accepts_lord_answer_on_consequence))]
    [HarmonyPrefix]
    private static bool ConversationMercenaryPlayerAcceptsLordAnswerOnConsequencePrefix(
        LordConversationsCampaignBehavior __instance)
    {
        int mercenaryAwardFactor = __instance.GetMercenaryAwardFactor();
        if (ModInformation.IsClient)
        {
            var kingdom = Hero.OneToOneConversationHero.Clan.Kingdom;
            MessageBroker.Instance.Publish(__instance, new MercenaryServiceAccepted(kingdom, mercenaryAwardFactor, Clan.PlayerClan));
            return false;
        }

        return true;
    }

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_player_want_to_end_service_as_mercenary_on_consequence))]
    [HarmonyPrefix]
    private static bool ConversationPlayerWantToEndServiceAsMercenaryOnConsequencePrefix(
        LordConversationsCampaignBehavior __instance)
    {
        if (ModInformation.IsClient)
        {
            var kingdom = Hero.OneToOneConversationHero.Clan.Kingdom;
            MessageBroker.Instance.Publish(__instance, new MercenaryServiceDismissalAccepted(kingdom, Clan.PlayerClan));
            return false;
        }

        return true;
    }

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_mercenary_response_accept_on_consqequence))]
    [HarmonyPrefix]
    private static bool ConversationMercenaryResponseAcceptOnConsequencePrefix(LordConversationsCampaignBehavior __instance)
    {
        int num = Campaign.Current.Models.MinorFactionsModel.GetMercenaryAwardFactorToJoinKingdom(Hero.OneToOneConversationHero.Clan, (Kingdom)Hero.MainHero.MapFaction, true);
        var kingdom = (Kingdom)Hero.MainHero.MapFaction;
        var clan = Hero.OneToOneConversationHero.Clan;
        if (Hero.OneToOneConversationHero.Clan.IsUnderMercenaryService)
        {
            num *= 3;
            num /= 2;
            if (Hero.OneToOneConversationHero.Clan.MapFaction.IsKingdomFaction)
            {
                MessageBroker.Instance.Publish(__instance, new MercenaryServiceDismissalAccepted(kingdom, clan));
            }
        }
        MessageBroker.Instance.Publish(__instance, new MercenaryServiceAccepted(kingdom, num, clan));
        return false;
    }

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_player_leave_faction_accepted_on_consequence))]
    [HarmonyPrefix]
    private static bool ConversationPlayerLeaveFactionAcceptedOnConsequencePrefix(LordConversationsCampaignBehavior __instance)
    {
        var kingdom = Hero.OneToOneConversationHero.Clan.Kingdom;
        if (Clan.PlayerClan.IsUnderMercenaryService)
        {
            MessageBroker.Instance.Publish(__instance, new MercenaryServiceDismissalAccepted(kingdom, Clan.PlayerClan));
            return false;
        }
        MessageBroker.Instance.Publish(__instance, new VassalServiceLeft(Clan.PlayerClan));
        return false;
    }

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_player_want_to_fire_mercenary_on_consequence))]
    [HarmonyPrefix]
    private static bool ConversationPlayerWantToFireMercenaryOnConsequencePrefix(LordConversationsCampaignBehavior __instance)
    {
        var kingdom = Hero.OneToOneConversationHero.Clan.Kingdom;
        var clan = Hero.OneToOneConversationHero.Clan;

        MessageBroker.Instance.Publish(__instance, new PlayerRelationChange(Hero.OneToOneConversationHero.Clan.Leader, -2));
        MessageBroker.Instance.Publish(__instance, new MercenaryServiceDismissalAccepted(kingdom, clan));
        return false;
    }

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_player_want_to_fire_mercenary_without_paying_debt_on_consequence))]
    [HarmonyPrefix]
    private static bool ConversationPlayerWantToFireMercenaryWithoutPayingDebtOnConsequencePrefix(LordConversationsCampaignBehavior __instance)
    {
        int num = MathF.Max(0, (int)Hero.OneToOneConversationHero.Clan.Influence) * Hero.OneToOneConversationHero.Clan.MercenaryAwardMultiplier;
        var kingdom = Hero.OneToOneConversationHero.Clan.Kingdom;
        var clan = Hero.OneToOneConversationHero.Clan;
        MessageBroker.Instance.Publish(__instance, new PlayerRelationChange(Hero.OneToOneConversationHero.Clan.Leader, -(int)(3f + MathF.Sqrt((float)((int)((float)num / 100f))))));
        MessageBroker.Instance.Publish(__instance, new MercenaryServiceDismissalAccepted(kingdom, clan));
        MessageBroker.Instance.Publish(__instance, new ChangeClanInfluence(Hero.OneToOneConversationHero.Clan, (int)(Hero.OneToOneConversationHero.Clan.Influence)));
        return false;
    }
    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_player_want_to_fire_mercenary_with_paying_debt_on_consequence))]
    [HarmonyPrefix]
    private static bool ConversationPlayerWantToFireMercenaryWithPayingDebtOnConsequence(LordConversationsCampaignBehavior __instance)
    {
        int amount = MathF.Max(0, (int)Hero.OneToOneConversationHero.Clan.Influence) * Hero.OneToOneConversationHero.Clan.MercenaryAwardMultiplier;
        MessageBroker.Instance.Publish(__instance, new GiveGold(amount, Hero.OneToOneConversationHero.Clan.Leader));
        MessageBroker.Instance.Publish(__instance, new ChangeClanInfluence(Hero.OneToOneConversationHero.Clan, (int)(Hero.OneToOneConversationHero.Clan.Influence)));
        return false;
    }
}
