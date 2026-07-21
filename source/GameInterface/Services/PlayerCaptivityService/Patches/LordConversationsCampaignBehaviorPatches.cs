using Common;
using Common.Messaging;
using GameInterface.Services.PlayerCaptivityService.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.PlayerCaptivityService.Patches;

/// <summary>
/// Routes the noble-liberation relation reward through the authoritative server.
/// </summary>
[HarmonyPatch(typeof(LordConversationsCampaignBehavior))]
internal class LordConversationsCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_player_liberates_prisoner_on_consequence))]
    [HarmonyPrefix]
    private static void ConversationPlayerLiberatesPrisonerOnConsequencePrefix(
        LordConversationsCampaignBehavior __instance)
    {
        if (ModInformation.IsServer || Hero.OneToOneConversationHero == null) return;

        MessageBroker.Instance.Publish(
            __instance,
            new PrisonerLiberationAttempted(Hero.OneToOneConversationHero));
    }
}
