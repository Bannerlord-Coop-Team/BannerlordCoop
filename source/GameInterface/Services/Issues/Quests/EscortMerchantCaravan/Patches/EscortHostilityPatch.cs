using Common;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Quests.EscortMerchantCaravan.Patches;

[HarmonyPatch(typeof(BeHostileAction), nameof(BeHostileAction.ApplyEncounterHostileAction))]
internal class EscortHostilityPatch
{
    [ThreadStatic] internal static PartyBase Attacker;

    [HarmonyPrefix]
    private static void Prefix(PartyBase attackerParty, out PartyBase __state)
    {
        __state = Attacker;
        if (ModInformation.IsServer) Attacker = attackerParty;
    }

    [HarmonyFinalizer]
    private static void Finalizer(PartyBase __state) => Attacker = __state;
}
