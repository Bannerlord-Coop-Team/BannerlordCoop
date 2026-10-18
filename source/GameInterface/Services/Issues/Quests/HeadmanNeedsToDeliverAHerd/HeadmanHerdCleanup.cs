using Common;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.SaveSystem;
using Behavior = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd;

internal interface IHeadmanHerdCleanup : IGameAbstraction
{
    void Remember(IssueBase issue, MobileParty party);
    bool HasRememberedParty(IssueBase issue);
    bool ReturnAlternativeTroops(IssueBase issue);
    void RemoveRemaining(Quest quest);
    void Finalize(IssueBase issue);
    void SyncData(IDataStore dataStore);
}

internal class HeadmanHerdCleanup : IHeadmanHerdCleanup
{
    private Dictionary<string, MobileParty> parties = new Dictionary<string, MobileParty>();

    public void Remember(IssueBase issue, MobileParty party) => parties[issue.StringId] = party;

    public bool HasRememberedParty(IssueBase issue) => parties.ContainsKey(issue.StringId);

    public bool ReturnAlternativeTroops(IssueBase issue)
    {
        if (!parties.TryGetValue(issue.StringId, out var party) || party == null)
            return false;

        foreach (var element in issue.AlternativeSolutionSentTroops.GetTroopRoster())
        {
            if (element.Character.IsHero)
                element.Character.HeroObject.ChangeState(Hero.CharacterStates.Active);
        }

        party.MemberRoster.Add(issue.AlternativeSolutionSentTroops);
        return true;
    }

    public void Finalize(IssueBase issue)
    {
        if (issue.IssueQuest is Quest quest && quest.IsOngoing)
            RemoveRemaining(quest);
        parties.Remove(issue.StringId);
    }

    public void RemoveRemaining(Quest quest)
    {
        if (parties.TryGetValue(IssueManager.GetIssueOfQuest(quest)?.StringId ?? string.Empty, out var party) && party != null)
        {
            var remaining = quest._animalCountToDeliver;
            foreach (var element in party.ItemRoster.ToArray())
            {
                if (element.EquipmentElement.Item != quest._herdTypeToDeliver) continue;
                var removed = Math.Min(remaining, element.Amount);
                party.ItemRoster.AddToCounts(element.EquipmentElement, -removed);
                remaining -= removed;
                if (remaining == 0) break;
            }
        }
    }

    public void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("coop_herd_cleanup_parties", ref parties);
        if (parties == null || ModInformation.IsClient) parties = new Dictionary<string, MobileParty>();
    }
}

[HarmonyPatch(typeof(Behavior), nameof(Behavior.SyncData))]
internal class HeadmanHerdCleanupSavePatch
{
    [HarmonyPostfix]
    private static void Postfix(IDataStore dataStore)
    {
        if (ContainerProvider.TryResolve<IHeadmanHerdCleanup>(out var cleanup)) cleanup.SyncData(dataStore);
    }
}

