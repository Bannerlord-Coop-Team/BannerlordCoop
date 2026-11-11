using Common;
using Common.Messaging;
using GameInterface.Services.Incidents.Interfaces;
using GameInterface.Services.Incidents.Messages;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Incidents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Incidents.Patches;

[HarmonyPatch(typeof(IncidentsCampaignBehaviour))]
internal class IncidentsCampaignBehaviourPatches
{
    [HarmonyPatch(nameof(IncidentsCampaignBehaviour.OnHourlyTick))]
    [HarmonyPrefix]
    public static bool OnHourlyTickPrefix()
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IIncidentsCampaignBehaviourInterface>(out var incidentsCampaignBehaviourInterface)) return false;

        incidentsCampaignBehaviourInterface.OnHourlyTick();

        return false;
    }

    [HarmonyPatch(nameof(IncidentsCampaignBehaviour.OnGameMenuOptionSelected))]
    [HarmonyPrefix]
    public static bool OnGameMenuOptionSelectedPrefix(IncidentsCampaignBehaviour __instance, GameMenu gameMenu, GameMenuOption option)
    {
        // Only settlement menus can trigger incidents, so skip sending the rest
        if (gameMenu.StringId is not ("town" or "castle" or "village")) return false;

        // Send to server to run interface version of method
        var message = new IncidentsGameMenuOptionSelected(Hero.MainHero, MobileParty.MainParty, gameMenu.StringId, option.IdString);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(IncidentsCampaignBehaviour.OnGameMenuOpened))]
    [HarmonyPrefix]
    public static bool OnGameMenuOpenedPrefix(IncidentsCampaignBehaviour __instance, MenuCallbackArgs args)
    {
        // Only settlement menus can trigger incidents, so skip sending the rest
        if (args.MenuContext.GameMenu.StringId is not ("town" or "castle" or "village")) return false;

        // Send to server to run interface version of method
        var message = new IncidentsGameMenuOpened(Hero.MainHero, MobileParty.MainParty, args.MenuContext.GameMenu.StringId);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(IncidentsCampaignBehaviour.OnMapEventEnded))]
    [HarmonyPrefix]
    public static bool OnMapEventEndedPrefix(MapEvent evt)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IIncidentsCampaignBehaviourInterface>(out var incidentsCampaignBehaviourInterface)) return false;

        incidentsCampaignBehaviourInterface.OnMapEventEnded(evt);

        return false;
    }

    [HarmonyPatch(nameof(IncidentsCampaignBehaviour.ConversationEnded))]
    [HarmonyPrefix]
    public static bool ConversationEndedPrefix(IncidentsCampaignBehaviour __instance, IEnumerable<CharacterObject> conversationCharacters)
    {
        if (PlayerEncounter.Current == null || !PlayerEncounter.LeaveEncounter) return false;

        // Send to server to run interface version of method
        var message = new IncidentsConversationEnded(Hero.MainHero, MobileParty.MainParty);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    // Handler subscribes to PlayerHeirSelectionCompleted directly
    [HarmonyPatch(nameof(IncidentsCampaignBehaviour.OnHeirSelectionOver))]
    [HarmonyPrefix]
    public static bool OnHeirSelectionOverPrefix() => false;

    [HarmonyPatch(nameof(IncidentsCampaignBehaviour.OnSettlementEntered))]
    [HarmonyPrefix]
    public static bool OnSettlementEnteredPrefix(MobileParty mobileParty)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IIncidentsCampaignBehaviourInterface>(out var incidentsCampaignBehaviourInterface)) return false;

        incidentsCampaignBehaviourInterface.OnSettlementEntered(mobileParty);

        return false;
    }

    [HarmonyPatch(nameof(IncidentsCampaignBehaviour.OnSettlementLeft))]
    [HarmonyPrefix]
    public static bool OnSettlementLeftPrefix(MobileParty mobileParty)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IIncidentsCampaignBehaviourInterface>(out var incidentsCampaignBehaviourInterface)) return false;

        incidentsCampaignBehaviourInterface.OnSettlementLeft(mobileParty);

        return false;
    }

}

// v1.5 moved OnIncidentResolved into IncidentManager.IncidentResolved, which keeps one global cooldown.
// IncidentsHandler keeps the cooldowns per player instead.
[HarmonyPatch(typeof(IncidentManager))]
internal class IncidentManagerResolvedPatches
{
    [HarmonyPatch(nameof(IncidentManager.IncidentResolved))]
    [HarmonyPrefix]
    public static bool IncidentResolvedPrefix() => false;
}
