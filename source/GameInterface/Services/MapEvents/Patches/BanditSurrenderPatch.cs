using Common;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// When a player accepts a bandit surrender via dialogue, the conversing client first sets the
/// encounter's victory state (SetOverrideWinner) and only then flags the enemy as surrendered
/// (EnemySurrender). The co-op server captures the defeated troops the moment the victory state
/// replicates, which is before the surrender is forwarded — so it would capture at the reduced
/// non-surrender rate. This marks that window so the victory-state relay is held back
/// (see <see cref="MapEventPatches"/>); the surrender is forwarded as its own message and the server
/// applies the whole surrender authoritatively, flagging the side as surrendered before it captures.
/// </summary>
[HarmonyPatch(typeof(BanditInteractionsCampaignBehavior))]
internal static class BanditSurrenderPatch
{
    [ThreadStatic]
    private static bool inSurrenderConsequence;

    /// <summary>True while the conversing client is applying a bandit-surrender dialogue consequence.</summary>
    internal static bool InSurrenderConsequence => inSurrenderConsequence;

    [HarmonyPatch("conversation_bandits_surrender_on_consequence")]
    [HarmonyPrefix]
    private static void Prefix()
    {
        if (ModInformation.IsClient)
            inSurrenderConsequence = true;
    }

    [HarmonyPatch("conversation_bandits_surrender_on_consequence")]
    [HarmonyFinalizer]
    private static void Finalizer()
    {
        inSurrenderConsequence = false;
    }
}
