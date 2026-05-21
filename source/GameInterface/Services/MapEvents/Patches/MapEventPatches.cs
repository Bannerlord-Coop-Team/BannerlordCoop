using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEventSides.Patches;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(MapEvent))]
internal class MapEventPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventPatches>();

    private static ConditionalWeakTable<MapEvent, JoinTimeLimit> joinLimits = new();

    class JoinTimeLimit
    {
        const int DelayHours = 5;

        public CampaignTime TimeLimit;
        public JoinTimeLimit()
        {
            TimeLimit = CampaignTime.HoursFromNow(DelayHours);
        }
    }

    [HarmonyPatch(nameof(MapEvent.AddInvolvedPartyInternal))]
    [HarmonyPrefix]
    private static void PrefixAddInvolvedPartyInternal(MapEvent __instance, MapEventParty mapEventParty)
    {
        // Parties not controlled by the server are player parties
        if (mapEventParty.Party.MobileParty.IsPlayerParty())
        {
            var partiesAdded = new List<MapEventParty>();

            __instance.TroopUpgradeTracker = new TroopUpgradeTracker();
            MapEventSide[] sides = __instance._sides;
            for (int i = 0; i < sides.Length; i++)
            {
                foreach (var existingParty in sides[i].Parties)
                {
                    __instance.TroopUpgradeTracker.AddParty(existingParty);
                    partiesAdded.Add(existingParty);
                }
            }

            var message = new MapEventInvolvedPartiesAdded(__instance, partiesAdded);
            MessageBroker.Instance.Publish(__instance, message);
        }
    }

    [HarmonyPatch(nameof(MapEvent.BattleState), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool PrefixBattleState(MapEvent __instance, BattleState value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            return true;
        }

        if (ModInformation.IsServer)
        {
            return true;
        }

        var message = new MapEventBattleStateChangeAttempted(__instance, value);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    [HarmonyPatch(nameof(MapEvent.OnBattleWon))]
    [HarmonyPrefix]
    private static void PrefixOnBattleWon(MapEvent __instance)
    {
        var containsPlayer = __instance._sides.Any(side => side.Parties.Any(party => party.Party.MobileParty.IsPlayerParty()));

        if (ModInformation.IsClient)
        {
            Logger.Error("Client called {MethodName}", nameof(MapEvent.OnBattleWon));
            return;
        }

        if (__instance.ContainsPlayerParty())
        {
            __instance.CalculateAndCommitMapEventResults();
        }
    }

    private static void PostfixCanPartyJoinBattle(MapEvent __instance, PartyBase party, ref bool __result)
    {
        // Return if party cannot join battle
        if (!__result)
            return;

        var joinLimit = joinLimits.GetOrCreateValue(__instance);

        // Prevent other parties from joining existing battles after a certain time limit
        if (CampaignTime.Now > joinLimit.TimeLimit)
        {
            __result = false;
            return;
        }

        if (party.MobileParty is null) return;

        // Disable other player joining battles for now
        if (party.MobileParty.IsPlayerParty() && __instance.ContainsPlayerParty())
        {
            __result = false;
            return;
        }
    }
}
