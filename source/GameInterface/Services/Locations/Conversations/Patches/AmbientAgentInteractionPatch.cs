using Common;
using GameInterface.Services.Locations.Patches;
using HarmonyLib;
using SandBox.Conversation.MissionLogics;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations.Conversations.Patches;

/// <summary>
/// Makes re-enabled ambient crowd agents non-interactable on clients, so clicking one does nothing instead of
/// opening vanilla's catch-all "I am not allowed to talk with you." line (ambient culture templates have no
/// real dialog). Synced hero NPCs (which are conversation-locked) and other interactable non-hero NPCs such as
/// the mercenary recruit and board-game host are unaffected, because only the re-enabled crowd is marked in
/// <see cref="AmbientCrowd"/>. Mirrors vanilla's own use of the private <c>_uninteractableAgents</c> set.
/// </summary>
[HarmonyPatch(typeof(MissionConversationLogic), nameof(MissionConversationLogic.OnAgentBuild))]
internal static class AmbientAgentInteractionPatch
{
    private static readonly AccessTools.FieldRef<MissionConversationLogic, HashSet<Agent>> UninteractableAgents =
        AccessTools.FieldRefAccess<MissionConversationLogic, HashSet<Agent>>("_uninteractableAgents");

    static void Postfix(MissionConversationLogic __instance, Agent agent)
    {
        if (!ModInformation.IsClient) return;
        if (agent == null || !agent.IsHuman) return;
        if (!(agent.Character is CharacterObject character) || !AmbientCrowd.IsAmbient(character)) return;

        var uninteractable = UninteractableAgents(__instance);
        uninteractable?.Add(agent);
    }
}
