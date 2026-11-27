using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches;

// ConversationEnded is left on for clients, it is what marks town and tavern NPCs as met after talking to them
[HarmonyPatch(typeof(HeroKnownInformationCampaignBehavior))]
internal class HeroKnownInformationCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(HeroKnownInformationCampaignBehavior.OnDailyTickHero))]
    [HarmonyPrefix]
    private static bool OnDailyTickHeroPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(HeroKnownInformationCampaignBehavior.OnAgentJoinedConversation))]
    [HarmonyPrefix]
    private static bool OnAgentJoinedConversationPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(HeroKnownInformationCampaignBehavior.OnPlayerMetHero))]
    [HarmonyPrefix]
    private static bool OnPlayerMetHeroPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(HeroKnownInformationCampaignBehavior.OnHeroesMarried))]
    [HarmonyPrefix]
    private static bool OnHeroesMarriedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(HeroKnownInformationCampaignBehavior.OnHeroCreated))]
    [HarmonyPrefix]
    private static bool OnHeroCreatedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(HeroKnownInformationCampaignBehavior.OnGameLoadFinishedEvent))]
    [HarmonyPrefix]
    private static bool OnGameLoadFinishedEventPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(HeroKnownInformationCampaignBehavior.OnCharacterCreationIsOver))]
    [HarmonyPrefix]
    private static bool OnCharacterCreationIsOverPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(HeroKnownInformationCampaignBehavior.OnPlayerLearnsAboutHero))]
    [HarmonyPrefix]
    private static bool OnPlayerLearnsAboutHeroPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(HeroKnownInformationCampaignBehavior.OnNearbyPartyAddedToPlayerMapEvent))]
    [HarmonyPrefix]
    private static bool OnNearbyPartyAddedToPlayerMapEventPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(HeroKnownInformationCampaignBehavior.OnGameMenuChanged))]
    [HarmonyPrefix]
    private static bool OnGameMenuChangedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(HeroKnownInformationCampaignBehavior.OnAfterMissionStarted))]
    [HarmonyPrefix]
    private static bool OnAfterMissionStartedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(HeroKnownInformationCampaignBehavior.OnMapEventStarted))]
    [HarmonyPrefix]
    private static bool OnMapEventStartedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(HeroKnownInformationCampaignBehavior.OnPartyAttachedAnotherParty))]
    [HarmonyPrefix]
    private static bool OnPartyAttachedAnotherPartyPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(HeroKnownInformationCampaignBehavior.OnPlayerJoinedTournament))]
    [HarmonyPrefix]
    private static bool OnPlayerJoinedTournamentPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(HeroKnownInformationCampaignBehavior.OnMarriageOfferedToPlayer))]
    [HarmonyPrefix]
    private static bool OnMarriageOfferedToPlayerPrefix() => ModInformation.IsServer;
}
