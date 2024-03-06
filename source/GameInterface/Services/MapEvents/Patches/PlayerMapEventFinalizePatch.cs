﻿using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches
{
    [HarmonyPatch(typeof(PlayerEncounter))]
    public class PlayerMapEventFinalizePatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<PlayerMapEventFinalizePatch>();

        [HarmonyPrefix]
        [HarmonyPatch(nameof(PlayerEncounter.FinalizeBattle))]
        static bool PrefixFinalizeBattle() //TODO Sync player battle results
        {
            MessageBroker.Instance.Publish(MobileParty.MainParty, new BattleEnded(MobileParty.MainParty.StringId));

            return true;
        }
    }
}