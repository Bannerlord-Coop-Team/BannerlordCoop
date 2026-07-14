using Common;
using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Clans.Patches;

/// <summary>
/// Routes the mercenary-service conversation consequence through the authoritative server.
/// </summary>
[HarmonyPatch(typeof(LordConversationsCampaignBehavior))]
internal class MercenaryServiceConversationPatch
{
    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_mercenary_player_accepts_lord_answer_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationMercenaryPlayerAcceptsLordAnswerOnConsequencePrefix(
        LordConversationsCampaignBehavior __instance)
    {
        if (ModInformation.IsClient)
        {
            var kingdom = Hero.OneToOneConversationHero.Clan.Kingdom;
            MessageBroker.Instance.Publish(__instance, new MercenaryServiceAccepted(kingdom));
            return false;
        }

        return true;
    }
}
