using Common;
using GameInterface.Configuration;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Kingdoms.Patches;

/// <summary>
/// Releases the clans of a kingdom being destroyed instead of destroying them based on config.
/// </summary>
[HarmonyPatch(typeof(DestroyKingdomAction))]
internal class DestroyKingdomActionPatches
{
    [HarmonyPatch(nameof(DestroyKingdomAction.ApplyInternal))]
    [HarmonyPrefix]
    private static void ApplyInternalPrefix(Kingdom destroyedKingdom)
    {
        if (ModInformation.IsClient || ModConfigProvider.ModOptions.ClansAfterKingdomDestruction == ClansAfterKingdomDestructionMode.Vanilla) return;

        foreach (Clan clan in destroyedKingdom.Clans.ToList())
        {
            // The ruling clan of a kingdom leader dying without an heir is still destroyed by vanilla
            if (clan.IsEliminated || clan.Leader == null || (clan == destroyedKingdom.RulingClan && clan.Leader.DeathMark != KillCharacterAction.KillCharacterActionDetail.None)) continue;

            ChangeKingdomAction.ApplyByLeaveByKingdomDestruction(clan);
        }
    }
}
