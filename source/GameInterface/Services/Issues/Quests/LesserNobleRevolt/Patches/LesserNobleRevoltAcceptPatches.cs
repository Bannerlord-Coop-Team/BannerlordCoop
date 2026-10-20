using Common;
using GameInterface.Policies;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt.Patches;

[HarmonyPatch]
public static class LesserNobleRevoltAcceptPatches
{
    [HarmonyPatch(typeof(Quest), MethodType.Constructor,
        new[] { typeof(string), typeof(Hero), typeof(CampaignTime), typeof(int), typeof(TextObject) })]
    [HarmonyPrefix]
    private static void UseClientPartyPlaceholder(Quest __instance)
    {
        if (ModInformation.IsClient)
        {
            // Vanilla dereferences this field after spawning. The real server party is attached by the handoff message.
            __instance._lesserNobleParty = MobileParty.MainParty;
        }
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.SpawnLesserNobleParty))]
    [HarmonyPrefix]
    private static bool SpawnParty() => ModInformation.IsServer;

    // Runs before TakePrisonerActionPatches, whose later prefix is then skipped, so a protected owner is
    // neither captured nor parked through its PrisonerTaken postfix.
    [HarmonyPatch(typeof(TakePrisonerAction), nameof(TakePrisonerAction.ApplyInternal))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    internal static bool ProtectOwnerFromCapture(PartyBase capturerParty, Hero prisonerCharacter)
    {
        return CallOriginalPolicy.IsOriginalAllowed() ||
            ModInformation.IsClient ||
            !(LesserNobleRevoltWorldEventPatches.IsProtectedHero(prisonerCharacter) ||
              IsOwnRevoltParty(capturerParty, prisonerCharacter));
    }

    // Vanilla never lets the revolt party hold its owner, and destroying it later inside the owner's
    // scope would strand them, so the owner is not captured even in a battle another player started.
    internal static bool IsOwnRevoltParty(PartyBase capturerParty, Hero prisoner)
    {
        if (capturerParty == null || prisoner == null || Campaign.Current?.IssueManager == null ||
            !ContainerProvider.TryResolve<IIssueOwnerResolver>(out var ownerResolver))
        {
            return false;
        }

        return Campaign.Current.IssueManager.Issues.Values.Any(issue =>
            issue.IssueQuest is Quest quest && quest.IsOngoing &&
            quest._lesserNobleParty?.Party == capturerParty &&
            ownerResolver.TryResolveOwnerHero(issue, out var owner) && owner == prisoner);
    }
}
