using Common;
using GameInterface.Configuration;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>Server hooks for <see cref="PostBattleScreens"/>.</summary>
[HarmonyPatch]
internal class PostBattleScreensPatches
{
    // Finalize detaches every party, so the players are read before vanilla runs.
    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.FinalizeEventAux))]
    [HarmonyPrefix]
    private static void Prefix_FinalizeEventAux(MapEvent __instance, bool __runOriginal)
    {
        if (!__runOriginal || ModInformation.IsClient || __instance.IsFinalized) return;
        if (!ContainerProvider.TryResolve<PostBattleScreens>(out var screens)) return;

        foreach (var party in __instance.InvolvedParties)
        {
            if (party.MobileParty?.IsPlayerParty() == true)
                screens.Enter(party.MobileParty, __instance);
        }
    }

    // The hard stop: OnPartyInteraction, which starts the encounter, only runs when this passes.
    [HarmonyPatch(typeof(PartyBase), "TaleWorlds.CampaignSystem.Map.IInteractablePoint.CanPartyInteract")]
    [HarmonyPostfix]
    private static void Postfix_CanPartyInteract(PartyBase __instance, MobileParty mobileParty, ref bool __result)
    {
        if (__result && IsProtectedFromAi(__instance.MobileParty, mobileParty))
            __result = false;
    }

    // Keeps the AI from chasing a player it can't engage yet.
    [HarmonyPatch(typeof(DefaultMobilePartyAIModel), nameof(DefaultMobilePartyAIModel.ShouldConsiderAttacking))]
    [HarmonyPostfix]
    private static void Postfix_ShouldConsiderAttacking(MobileParty party, MobileParty targetParty, ref bool __result)
    {
        if (__result && IsProtectedFromAi(targetParty, party))
            __result = false;
    }

    // Both gates run for every AI decision, so the default (off) skips the container lookup.
    private static bool IsProtectedFromAi(MobileParty target, MobileParty attacker) =>
        ModConfigProvider.ModOptions.PostBattleAiAmnestySeconds > 0 &&
        ModInformation.IsServer &&
        ContainerProvider.TryResolve<PostBattleScreens>(out var screens) &&
        screens.IsProtectedFrom(target, attacker);
}
