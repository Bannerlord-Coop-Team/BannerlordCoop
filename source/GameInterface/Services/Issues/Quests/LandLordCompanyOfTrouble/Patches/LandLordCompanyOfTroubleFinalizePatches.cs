using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble.Patches;

[HarmonyPatch]
internal class LandLordCompanyOfTroubleFinalizePatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFinalize))]
    [HarmonyPrefix]
    private static bool OnFinalizePrefix()
    {
        return ModInformation.IsServer;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFinalize))]
    [HarmonyPostfix]
    private static void OnFinalizePostfix(Quest __instance)
    {
        var company = __instance._companyOfTroubleParty;
        if (ModInformation.IsClient || company == null)
        {
            return;
        }

        GameThread.RunSafe(() => RemoveCompany(company), context: nameof(LandLordCompanyOfTroubleFinalizePatches));
    }

    private static void RemoveCompany(MobileParty company)
    {
        if (!company.IsActive)
        {
            return;
        }

        if (company.MapEvent != null)
        {
            company.Ai.SetDoNotMakeNewDecisions(false);
            company.SetPartyUsedByQuest(false);
            return;
        }

        DestroyPartyAction.Apply(null, company);
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPrefix]
    private static void IssueFinalizedPrefix(IssueBase __instance)
    {
        if (ModInformation.IsClient || !(__instance.IssueQuest is Quest quest) || !quest.IsOngoing)
        {
            return;
        }

        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) ||
            !ContainerProvider.TryResolve<IIssueOwnerResolver>(out var ownerResolver))
        {
            return;
        }

        if (!ownerResolver.TryResolveOwnerHero(__instance, out _))
        {
            RemoveLeftovers(quest);
            return;
        }

        dispatcher.RunAsOwners<Quest>(ownedQuest =>
        {
            if (ownedQuest == quest)
            {
                ownedQuest.OnFinalize();
            }
        });
    }

    private static void RemoveLeftovers(Quest quest)
    {
        foreach (var party in MobileParty.All.ToList())
        {
            var count = party.MemberRoster.GetTroopCount(quest._troubleCharacterObject);
            if (party != quest._companyOfTroubleParty && count > 0)
            {
                party.MemberRoster.AddToCounts(quest._troubleCharacterObject, -count);
            }
        }

        if (quest._companyOfTroubleParty != null)
        {
            RemoveCompany(quest._companyOfTroubleParty);
        }
    }
}
