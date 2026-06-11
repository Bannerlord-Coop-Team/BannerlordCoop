using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages.Start;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

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
            var requestBattleMessgae = new StartBattleAttempted(attackerParty, defenderParty, subject as Settlement, battleType);
            MessageBroker.Instance.Publish(null, requestBattleMessgae);
            return false;
        }

        return true;
    }
}
