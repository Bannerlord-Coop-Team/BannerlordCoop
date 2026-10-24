using Common;
using Common.Util;
using HarmonyLib;
using Helpers;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters.Patches;

/// <summary>World creation and cleanup belong to the server, encounters to the owning client.</summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class ExtortionByDesertersPartyPatches
{
    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddTrackedObject))]
    [HarmonyPrefix]
    private static bool AddTrackedObjectPrefix(QuestBase __instance, ITrackableCampaignObject trackedObject)
    {
        // Its constructor runs before the server's party binding arrives.
        return !(__instance is Quest) || trackedObject != null;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.StartAmbushEncounter))]
    [HarmonyPrefix]
    private static bool StartAmbushEncounterPrefix(Quest __instance)
    {
        var raid = __instance._deserterMobileParty?.MapEvent;
        if (ModInformation.IsServer || raid == null || __instance._defenderMobileParty == null ||
            MobileParty.MainParty.MapEvent != raid) return false;

        // Adopt the server's raid; vanilla's full ambush method would add client-created battle parties.
        using (new AllowedThread())
        {
            // Finishing an encounter already using this raid would finalize its battle locally.
            if (PlayerEncounter.Battle != raid)
            {
                if (PlayerEncounter.Current != null)
                {
                    PlayerEncounter.Current._mapEvent = null;
                    PlayerEncounter.Finish(forcePlayerOutFromSettlement: false);
                }
                PlayerEncounter.Start();
            }
            PlayerEncounter.Init();
            var leader = ConversationHelper.GetConversationCharacterPartyLeader(__instance._deserterMobileParty.Party);
            var player = new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty, noHorse: true);
            var deserter = new ConversationCharacterData(leader, __instance._deserterMobileParty.Party, noHorse: true);
            GameMenu.ActivateGameMenu("encounter_meeting");
            CampaignMapConversation.OpenConversation(player, deserter);
        }
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnBattleStarted))]
    [HarmonyPrefix]
    private static bool OnBattleStartedPrefix() => false;

    // The server cleans up the battle parties in its FinalizeEventAux postfix, the only caller of OnMapEventEnded
    [HarmonyPatch(typeof(Quest), nameof(Quest.GameMenuOpened))]
    [HarmonyPrefix]
    private static bool GameMenuOpenedPrefix() => false;

    // Dismissing an issue drops its running quest unfinished, which would leave the deserters and defenders on the map
    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPrefix]
    private static void IssueFinalizedPrefix(IssueBase __instance)
    {
        if (ModInformation.IsServer && __instance.IssueQuest is Quest quest && quest.IsOngoing) quest.OnFinalize();
    }
}

/// <summary>These world methods must stay server-only even during client replay.</summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class ExtortionByDesertersServerMethodsPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.DestroyDeserterParty));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.DestroyDefenderParty));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.HourlyTick));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.GameMenuOptionSelected));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnWarDeclared));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnClanChangedKingdom));
    }

    [HarmonyPrefix]
    internal static bool Prefix() => ModInformation.IsServer;
}
