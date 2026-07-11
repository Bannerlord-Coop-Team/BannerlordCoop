using Common;
using Common.Messaging;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Announces a siege assault starting on the server so defending players get their encounter menu.
/// The vanilla pull-in runs inside the attacker's interaction, which never executes on a defending
/// client (the attacker party is not controlled there) nor usefully on the headless host (no main
/// party in a settlement), so without this no defender is ever prompted into the walls mission.
/// </summary>
[HarmonyPatch(typeof(StartBattleAction))]
internal class SiegeAssaultPromptPatches
{
    [HarmonyPatch(nameof(StartBattleAction.ApplyInternal))]
    [HarmonyPostfix]
    private static void ApplyInternalPostfix(PartyBase attackerParty, Settlement subject, MapEvent.BattleTypes battleType)
    {
        if (ModInformation.IsClient) return;
        if (battleType != MapEvent.BattleTypes.Siege || subject == null) return;
        if (attackerParty?.MobileParty == null) return;

        MessageBroker.Instance.Publish(null, new SiegeAssaultStarted(attackerParty.MobileParty, subject));
    }
}
