using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Patches for player encounters
/// </summary>

[HarmonyPatch(typeof(EncounterManager))]
internal class EncounterPatches
{
    private static ILogger Logger = LogManager.GetLogger<EncounterManagerPatches>();

    [HarmonyPrefix]
    [HarmonyPatch(nameof(EncounterManager.Tick))]
    internal static bool TickPatch(float dt)
    {
        return ModInformation.IsServer;
    }

    private static string lastAttackerPartyId;

    [HarmonyPrefix]
    [HarmonyPatch(nameof(EncounterManager.StartPartyEncounter))]
    static bool Prefix(PartyBase attackerParty, PartyBase defenderParty)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;

        if (lastAttackerPartyId == attackerParty.MobileParty.StringId) return false;
        lastAttackerPartyId = attackerParty.MobileParty.StringId;


        if (ModInformation.IsClient && (attackerParty.MobileParty == MobileParty.MainParty || defenderParty.MobileParty == MobileParty.MainParty))
        {
            MessageBroker.Instance.Publish(attackerParty, new BattleStarted(
                attackerParty.MobileParty,
                defenderParty.MobileParty));

            return false;
        }

        // Disables interaction between players, this will be handled in a future issue
        if (!attackerParty.MobileParty.IsPartyControlled() && !defenderParty.MobileParty.IsPartyControlled()) { return false; }

        return true;
    }

    //[HarmonyPostfix]
    //[HarmonyPatch(nameof(EncounterManager.StartPartyEncounter))]
    //static void Postfix(PartyBase attackerParty, PartyBase defenderParty)
    //{
    //    if (ModInformation.IsClient) return;

    //    if (AllowedThread.IsThisThreadAllowed()) return;

    //    MessageBroker.Instance.Publish(attackerParty, new BattleStarted(
    //        attackerParty.MobileParty,
    //        defenderParty.MobileParty));
    //}

    public static void OverrideOnPartyInteraction(MobileParty attacker, MobileParty defender)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                if (attacker == MobileParty.MainParty || defender == MobileParty.MainParty)
                {
                    MapState mapState = Game.Current.GameStateManager.ActiveState as MapState;
                    if (mapState != null)
                    {
                        mapState.OnMainPartyEncounter();
                    }
                }
                EncounterManager.StartPartyEncounter(attacker.Party, defender.Party);
            }
        }, true);
    }
}