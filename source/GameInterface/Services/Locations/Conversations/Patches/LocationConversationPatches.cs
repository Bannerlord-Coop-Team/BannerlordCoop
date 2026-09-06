using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Locations.Messages.Conversation;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using SandBox.Conversation.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations.Conversations.Patches;

/// <summary>
/// Client-side acquire/release for the location-conversation lock. Pending and held state lives in the
/// active client's lifetime scope so in-process clients cannot overwrite one another.
/// </summary>
[HarmonyPatch]
internal static class LocationConversationPatches
{
    [HarmonyPatch(typeof(MissionConversationLogic), nameof(MissionConversationLogic.OnAgentInteraction))]
    [HarmonyPrefix]
    static bool OnAgentInteractionPrefix(MissionConversationLogic __instance, Agent userAgent, Agent agent)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (!ModInformation.IsClient) return true;
        if (!TryGetLockTarget(__instance, userAgent, agent, out var locationId, out var characterId)) return true;
        if (!ContainerProvider.TryResolve<ILocationConversationClientState>(out var state)) return true;

        if (!state.TryBeginPending(agent, locationId, characterId, out var generation)) return false;

        MessageBroker.Instance.Publish(agent, new LocationConversationRequested(locationId, characterId, generation));
        return false;
    }

    private static bool TryGetLockTarget(
        MissionConversationLogic instance,
        Agent userAgent,
        Agent agent,
        out string locationId,
        out string characterId)
    {
        locationId = null;
        characterId = null;

        if (!(agent?.Character is CharacterObject character) || !character.IsHero) return false;

        var conversationManager = instance.ConversationManager;
        if (conversationManager == null || conversationManager.IsConversationInProgress) return false;
        if (!instance.IsThereAgentAction(userAgent, agent)) return false;

        var location = CampaignMission.Current?.Location;
        if (location == null || !ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return false;

        return objectManager.TryGetId(character, out characterId) &&
               objectManager.TryGetId(location, out locationId);
    }

    [HarmonyPatch(typeof(MissionConversationLogic), "OnConversationEnd")]
    [HarmonyPostfix]
    static void OnConversationEndPostfix()
    {
        if (!ContainerProvider.TryResolve<ILocationConversationClientState>(out var state)) return;
        if (state.HeldNpcKey == null) return;

        state.ClearHeld();
        MessageBroker.Instance.Publish(null, new LocationConversationEnded());
    }

    [HarmonyPatch(typeof(MissionConversationLogic), "OnBehaviorInitialize")]
    [HarmonyPostfix]
    static void OnBehaviorInitializePostfix()
    {
        ReleaseStaleLock();
    }

    [HarmonyPatch(typeof(MissionConversationLogic), "OnEndMission")]
    [HarmonyPostfix]
    static void OnEndMissionPostfix()
    {
        ReleaseStaleLock();
    }

    static void ReleaseStaleLock()
    {
        if (!ContainerProvider.TryResolve<ILocationConversationClientState>(out var state)) return;
        if (!state.Clear()) return;

        MessageBroker.Instance.Publish(null, new LocationConversationEnded());
    }

    internal static void StartApprovedConversation(
        ILocationConversationClientState state,
        int generation)
    {
        if (state == null || !state.TryTakePending(generation, out var pending)) return;

        var logic = Mission.Current != null ? MissionConversationLogic.Current : null;
        var conversationManager = Campaign.Current?.ConversationManager;

        if (logic == null || conversationManager == null || conversationManager.IsConversationInProgress ||
            pending.Agent == null || !pending.Agent.IsActive())
        {
            MessageBroker.Instance.Publish(null, new LocationConversationEnded());
            return;
        }

        state.Hold(LocationConversationTracker.ComposeKey(pending.LocationId, pending.CharacterId));
        try
        {
            logic.StartConversation(pending.Agent, setActionsInstantly: false);
        }
        catch
        {
            state.ClearHeld();
            MessageBroker.Instance.Publish(null, new LocationConversationEnded());
            throw;
        }
    }

    internal static bool CancelPending(
        ILocationConversationClientState state,
        int generation)
    {
        return state != null && state.CancelPending(generation);
    }
}
