using Common;
using Common.Logging;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using Quest = TaleWorlds.CampaignSystem.Issues.ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArmyNeedsSupplies.Patches;

[HarmonyPatch]
internal class ArmyNeedsSuppliesWarDeclaredPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<ArmyNeedsSuppliesWarDeclaredPatch>();

    [HarmonyPatch(typeof(DeclareWarAction), nameof(DeclareWarAction.ApplyInternal))]
    [HarmonyPostfix]
    private static void ApplyInternalPostfix(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        if (!ContainerProvider.TryResolve<IIssueOwnerResolver>(out var ownerResolver))
        {
            return;
        }

        var issues = Campaign.Current.IssueManager.Issues.Values
            .Where(issue => issue.IssueQuest is Quest quest && quest.IsOngoing)
            .ToList();

        foreach (var issue in issues)
        {
            try
            {
                if (!ownerResolver.TryResolveOwnerHero(issue, out var ownerHero))
                {
                    Logger.Warning("{issue} has a running quest but no owning player", issue.StringId);
                    continue;
                }

                using (new MainHeroSubstitutionScope(ownerHero, ownerHero.PartyBelongedTo))
                {
                    ((Quest)issue.IssueQuest).OnWarDeclared(faction1, faction2, declareWarDetail);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Checking the war against {issue} failed", issue.StringId);
            }
        }
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnWarDeclared))]
    [HarmonyPrefix]
    private static bool OnWarDeclaredPrefix()
    {
        return ModInformation.IsServer;
    }
}
