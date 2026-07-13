using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Clans.Patches;

/// <summary>
/// Routes the mercenary-service conversation consequence through the authoritative server.
/// </summary>
[HarmonyPatch(typeof(LordConversationsCampaignBehavior))]
internal class MercenaryServiceConversationPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<MercenaryServiceConversationPatch>();

    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_mercenary_player_accepts_lord_answer_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationMercenaryPlayerAcceptsLordAnswerOnConsequencePrefix(
        LordConversationsCampaignBehavior __instance)
    {
        if (ModInformation.IsClient)
        {
            var kingdom = Hero.OneToOneConversationHero?.Clan?.Kingdom;
            if (kingdom == null)
            {
                Logger.Error("Unable to request mercenary service because the conversation hero has no kingdom");
                return false;
            }

            MessageBroker.Instance.Publish(__instance, new MercenaryServiceAccepted(kingdom));
            return false;
        }

        return true;
    }
}
