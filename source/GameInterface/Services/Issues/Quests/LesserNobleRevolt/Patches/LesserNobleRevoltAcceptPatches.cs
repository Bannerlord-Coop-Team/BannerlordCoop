using Common;
using GameInterface.Policies;
using HarmonyLib;
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
            !ContainerProvider.TryResolve<ILesserNobleRevoltWorldEventService>(out var service) ||
            !(service.IsProtectedHero(prisonerCharacter) || service.IsOwnRevoltParty(capturerParty, prisonerCharacter));
    }
}
