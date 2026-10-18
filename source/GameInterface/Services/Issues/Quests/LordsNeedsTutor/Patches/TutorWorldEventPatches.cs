using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest;

namespace GameInterface.Services.Issues.Quests.LordsNeedsTutor.Patches;

[HarmonyPatch]
internal class TutorWorldEventPatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroKilled))]
    [HarmonyPostfix]
    private static void OnHeroKilledPostfix(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification) => Run(quest => quest.OnHeroKilled(victim, killer, detail, showNotification));

    // War and kingdom changes reach the quest through QuestEventRelay, which looks for OnHeroPrisonerTaken and so misses this quest's OnPrisonerTaken
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroPrisonerTaken))]
    [HarmonyPostfix]
    private static void OnHeroPrisonerTakenPostfix(PartyBase capturer, Hero prisoner) => Run(quest => quest.OnPrisonerTaken(capturer, prisoner));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroGainedSkill))]
    [HarmonyPostfix]
    private static void OnHeroGainedSkillPostfix(Hero hero, SkillObject skill, int change, bool shouldNotify) => Run(quest => quest.OnHeroGainedSkill(hero, skill, change, shouldNotify));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnClanLeaderChanged))]
    [HarmonyPostfix]
    private static void OnClanLeaderChangedPostfix(Hero oldLeader, Hero newLeader) => Run(quest => quest.OnClanLeaderChanged(oldLeader, newLeader));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnCompanionRemoved))]
    [HarmonyPostfix]
    private static void OnCompanionRemovedPostfix(Hero companion, RemoveCompanionAction.RemoveCompanionDetail detail) => Run(quest => quest.OnCompanionRemoved(companion, detail));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnBeforeHeroesMarried))]
    [HarmonyPostfix]
    private static void OnBeforeHeroesMarriedPostfix(Hero hero1, Hero hero2, bool showNotification) => Run(quest => quest.OnHeroesMarried(hero1, hero2, showNotification));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void OnMapEventStartedPostfix(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty) => Run(quest => quest.OnMapEventStarted(mapEvent, attackerParty, defenderParty));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnPerkReset))]
    [HarmonyPostfix]
    private static void OnPerkResetPostfix(Hero hero, PerkObject perk) => Run(quest => quest.OnPerkReset(hero, perk));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnNewCompanionAdded))]
    [HarmonyPostfix]
    private static void OnNewCompanionAddedPostfix(Hero newCompanion) => Run(quest => quest.OnNewCompanionAdded(newCompanion));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTick))]
    [HarmonyPostfix]
    private static void HourlyTickPostfix() => Run(quest =>
    {
        if (quest.QuestDueTime.IsPast) quest.CompleteQuestWithTimeOut();
    });

    private static void Run(Action<Quest> listener)
    {
        if (ModInformation.IsClient || !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) return;
        dispatcher.RunAsOwners<Quest>(quest =>
        {
            if (quest._startQuestLog == null) return;
            var count = quest.JournalEntries.Count;
            var progress = quest._startQuestLog.CurrentProgress;
            var ready = quest._questCompletedStartConversation;
            listener(quest);
            if ((count != quest.JournalEntries.Count || progress != quest._startQuestLog.CurrentProgress || ready != quest._questCompletedStartConversation) &&
                ContainerProvider.TryResolve<ITutorJournal>(out var journal)) journal.Send(quest);
        });
    }
}

[HarmonyPatch]
internal class TutorWorldListenerPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnHeroKilled));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnPrisonerTaken));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnHeroGainedSkill));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnWarDeclared));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnClanLeaderChanged));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnClanChangedKingdom));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnCompanionRemoved));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnHeroesMarried));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnMapEventStarted));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnPerkReset));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnNewCompanionAdded));
    }

    private static bool Prefix() => ModInformation.IsServer;
}
