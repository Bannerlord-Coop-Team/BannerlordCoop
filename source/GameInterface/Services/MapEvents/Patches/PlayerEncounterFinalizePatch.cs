using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Patches
{
    //[HarmonyPatch(typeof(PlayerEncounter))]
    //public class PlayerEncounterFinalizePatch
    //{
    //    private static readonly ILogger Logger = LogManager.GetLogger<PlayerEncounterFinalizePatch>();

    //    [HarmonyPrefix]
    //    [HarmonyPatch(nameof(PlayerEncounter.FinalizeBattle))]
    //    static bool PrefixFinalizeBattle() //TODO Sync player battle results
    //    {
    //        MessageBroker.Instance.Publish(MobileParty.MainParty, new BattleEnded(MobileParty.MainParty.StringId));

    //        return true;
    //    }
    //}
}