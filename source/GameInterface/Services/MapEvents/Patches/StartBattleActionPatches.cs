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
        // Server-side direct AI encounters can reach StartBattleAction without
        // calling MapEvent.CanPartyJoinBattle. Reject before native mutates the
        // side and before OnStartBattle assumes that mutation succeeded.
        if (InteractionPatches.TrySuppressExpiredReinforcement(
                attackerParty, defenderParty))
        {
            return false;
        }

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
