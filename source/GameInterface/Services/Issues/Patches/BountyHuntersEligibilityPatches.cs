using Common;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Issue = CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue;

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueQuestCanBeDuplicated), MethodType.Getter)]
internal class BountyHuntersEligibilityPatches
{
    [HarmonyPostfix]
    private static void OnlyCountThisPlayersCommitments(IssueBase __instance, ref bool __result)
    {
        if (__instance is not Issue || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return;
        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership)) return;
        string controllerId = null;
        if (ModInformation.IsServer)
        {
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objects)
                || !objects.TryGetIdWithLogging(Hero.MainHero, out var heroId)
                || !ContainerProvider.TryResolve<IPlayerManager>(out var players)) return;
            controllerId = players.Players.FirstOrDefault(player => player.HeroId == heroId)?.ControllerId;
        }
        else if (ContainerProvider.TryResolve<IControllerIdProvider>(out var controller))
        {
            controllerId = controller.ControllerId;
        }
        if (controllerId == null) return;
        __result = !Campaign.Current.IssueManager.Issues.Values.Any(issue => issue is Issue
            && (issue.IsSolvingWithQuest || issue.IsSolvingWithAlternative)
            && ownership.TryGetOwnerControllerId(issue.IssueOwner, out var owner) && owner == controllerId);
    }
}
