using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

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

        [HarmonyPrefix]
        [HarmonyPatch("FinishBattle")]
        static bool PrefixFinishBattle(MapEvent __instance)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;

            if (ModInformation.IsClient) return false;

            if (__instance.InvolvedParties.Any(x => !x.MobileParty.IsPartyControlled()))
            {
                return false; //TODO Manage player party interactions
            }

            MobileParty party = __instance.InvolvedParties.First().MobileParty;

            MessageBroker.Instance.Publish(party, new BattleEnded(party.StringId));

            return false;
        }

        public static void OverrideFinishBattle(MapEvent mapEvent)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    mapEvent.FinishBattle();
                }
            });
        }
    }
}
