using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest;

namespace GameInterface.Services.Issues.Quests.CapturedByBountyHunters.Patches;

/// <summary>
/// Only the server finalizes map events, so it checks every running quest's hideout battle as its owning player
/// and applies the consequences there. A client only ends its copy of the quest once the server announces it.
/// </summary>
[HarmonyPatch]
internal class CapturedByBountyHuntersOutcomePatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPostfix]
    private static void OnMapEventEndedPostfix(MapEvent mapEvent)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.RunAsOwners<Quest>(quest =>
        {
            var owner = Hero.MainHero;

            // An owner without a party would be checked against the party of whoever ended the battle, so their two
            // vanilla branches are decided here. Losing the hideout battle removes the owner's party before it ends.
            if (owner?.PartyBelongedTo == null)
            {
                if (mapEvent.MapEventSettlement == quest._questHideout && WasCapturedLosingThisAssault(owner, mapEvent))
                {
                    quest.AddLog(quest.PlayerLostTheFightLogText);
                    quest.FailConsequences(isTimedOut: false);
                }
                else if (quest._questHideout.Parties.Count == 0)
                {
                    quest.AddLog(quest.HideoutClearedBySomeoneElseLogText);
                    quest.CompleteQuestWithFail();
                }

                return;
            }

            quest.OnMapEventEnded(mapEvent);
        });
    }

    // Taken prisoner during this battle by the defenders who beat the assault, not merely held by someone who fought it
    private static bool WasCapturedLosingThisAssault(Hero owner, MapEvent mapEvent)
    {
        var captor = owner?.IsPrisoner == true ? owner.PartyBelongedToAsPrisoner : null;

        return captor?.MapEvent == mapEvent &&
            captor.Side == BattleSideEnum.Defender &&
            mapEvent.DefeatedSide == BattleSideEnum.Attacker &&
            owner.CaptivityStartTime >= mapEvent.BattleStartTime;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventEnded))]
    [HarmonyPrefix]
    private static bool OnMapEventEndedPrefix()
    {
        return ModInformation.IsServer;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.SuccessConsequences))]
    [HarmonyPrefix]
    private static bool SuccessConsequencesPrefix()
    {
        return ModInformation.IsServer;
    }

    // The client replaying a timeout reaches this too, the server already applied it
    [HarmonyPatch(typeof(Quest), nameof(Quest.FailConsequences))]
    [HarmonyPrefix]
    private static bool FailConsequencesPrefix()
    {
        return ModInformation.IsServer;
    }
}
