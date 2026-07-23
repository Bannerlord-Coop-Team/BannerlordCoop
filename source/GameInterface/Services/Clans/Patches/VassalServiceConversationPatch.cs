using Common;
using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;

namespace GameInterface.Services.Clans.Patches;

/// <summary>
/// Routes accepted vassalage through the authoritative server.
/// </summary>
[HarmonyPatch(typeof(LordConversationsCampaignBehavior))]
internal class VassalServiceConversationPatch
{
    [HarmonyPatch(nameof(LordConversationsCampaignBehavior.conversation_player_is_accepted_as_a_vassal_on_consequence))]
    [HarmonyPrefix]
    public static bool ConversationPlayerIsAcceptedAsVassalPrefix(LordConversationsCampaignBehavior __instance)
    {
        if (ModInformation.IsServer) return true;

        Kingdom kingdom = Hero.OneToOneConversationHero?.Clan?.Kingdom;
        if (kingdom != null)
        {
            MessageBroker.Instance.Publish(
                __instance,
                new VassalServiceAccepted(kingdom, !__instance._receivedVassalRewards));
        }

        if (PlayerEncounter.Current != null)
        {
            PlayerEncounter.LeaveEncounter = true;
        }

        return false;
    }
}
