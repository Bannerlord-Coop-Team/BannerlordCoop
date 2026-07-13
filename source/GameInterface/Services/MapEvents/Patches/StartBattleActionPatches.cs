using Common;
using GameInterface.Policies;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(StartBattleAction))]
internal class StartBattleActionPatches
{
    [HarmonyPatch(nameof(StartBattleAction.ApplyInternal))]
    [HarmonyPrefix]
    public static bool PrefixApply(PartyBase attackerParty, PartyBase defenderParty, object subject, MapEvent.BattleTypes battleType)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            // Clients cannot natively start a battle — the server drives battle start. Suppress the vanilla call.
            return false;
        }

        return true;
    }
}
