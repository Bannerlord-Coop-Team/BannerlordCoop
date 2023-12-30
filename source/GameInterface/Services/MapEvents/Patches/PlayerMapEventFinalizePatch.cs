using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
using Serilog.Core;
using System.Linq;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches
{
    [HarmonyPatch(typeof(PlayerEncounter))]
    public class PlayerMapEventFinalizePatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<PlayerMapEventFinalizePatch>();

        [HarmonyPrefix]
        [HarmonyPatch(nameof(PlayerEncounter.FinalizeBattle))]
        static bool PrefixFinalizeBattle()
        {
            MessageBroker.Instance.Publish(MobileParty.MainParty, new BattleEnded(MobileParty.MainParty.StringId));

            return true;
        }
    }
}
