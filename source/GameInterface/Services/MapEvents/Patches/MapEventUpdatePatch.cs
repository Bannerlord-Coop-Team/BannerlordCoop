using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
using Serilog.Core;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches
{
    [HarmonyPatch(typeof(MapEvent))]
    public class MapEventUpdatePatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<StartBattleHandler>();

        [HarmonyPrefix]
        [HarmonyPatch("FinishBattle")]
        static bool PrefixFinishBattle(MapEvent __instance)
        {
            if (ModInformation.IsClient) return false;

            if (__instance.InvolvedParties.Any(x => !x.MobileParty.IsPartyControlled()))
            {
                return false; //Player involved
            }

            MobileParty party = __instance.InvolvedParties.First().MobileParty;

            MessageBroker.Instance.Publish(party, new BattleEnded(party.StringId));

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("ApplyBattleResults")] //Make sure client cannot give itself rewards (Maybe needed?)
        static bool PrefixApplyBattleResults()
        {
            if (ModInformation.IsClient) return false;
            return true;
        }
    }
}
