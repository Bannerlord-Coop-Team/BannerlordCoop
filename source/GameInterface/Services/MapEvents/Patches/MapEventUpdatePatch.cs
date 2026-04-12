using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Helpers;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Patches
{
    [HarmonyPatch(typeof(MapEvent))]
    public class MapEventUpdatePatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        static bool PrefixUpdate(MapEvent __instance)
        {
            if (ModInformation.IsClient) return false;

            // Don't update if a player is involved
            // Prevents server from instantly finishing the battle and waits for client finish request
            if (__instance.InvolvedParties.Any(x => x.MobileParty.IsPartyControlled() == false)) return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(MapEvent))]
    public class MapEventUpdatePatch2
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(MapEvent.RecalculateRenownAndInfluenceValues))]
        static bool PrefixUpdate(MapEvent __instance, PartyBase party)
        {
            __instance.StrengthOfSide[(int)party.Side] += party.GetCustomStrength(party.Side, __instance.SimulationContext);
            MapEventSide[] sides = __instance._sides;
            for (int i = 0; i < sides.Length; i++)
            {
                if (sides[i] == null) continue;
                sides[i].CalculateRenownAndInfluenceValues(__instance.StrengthOfSide);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(MapEvent))]
    public class MapEventInitPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(MapEvent.Initialize))]
        static bool Prefix(MapEvent __instance, PartyBase attackerParty, PartyBase defenderParty, MapEventComponent component, MapEvent.BattleTypes mapEventType)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient) return false;

            MapEventInitialize message = new MapEventInitialize(__instance, mapEventType, attackerParty, defenderParty);

            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }
    }

    [HarmonyPatch(typeof(BattleSimulation))]
    public class BattleSimulationUpdatePatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(BattleSimulation.SimulateBattle))]
        static bool PrefixUpdate(BattleSimulation __instance)
        {
            return false;
        }
    }
}