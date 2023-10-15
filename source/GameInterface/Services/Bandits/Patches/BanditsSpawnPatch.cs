using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Bandits.Messages;
using GameInterface.Services.GameDebug.Patches;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Clans.Patches
{
    [HarmonyPatch(typeof(BanditsCampaignBehavior), "SpawnAPartyInFaction")]
    public class BanditsSpawnPatch
    {
        private static readonly AllowedInstance<Clan> AllowedInstance = new AllowedInstance<Clan>();

        private static readonly Action<Clan> SpawnBandits =
            typeof(BanditsCampaignBehavior)
            .GetMethod("SpawnAPartyInFaction", BindingFlags.NonPublic | BindingFlags.Static)
            .BuildDelegate<Action<Clan>>();
        static bool Prefix(Clan selectedFaction)
        {
            CallStackValidator.Validate(selectedFaction, AllowedInstance);

            if (AllowedInstance.IsAllowed(selectedFaction)) return true;

            if (ModInformation.IsClient) return false; //Block any spawning on clients unless allowed

            MessageBroker.Instance.Publish(selectedFaction, new BanditsSpawned(selectedFaction.StringId));

            return false;
        }

        public static void RunOriginalSpawnAPartyInFaction(Clan clan)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = clan;

                GameLoopRunner.RunOnMainThread(() =>
                {
                    SpawnBandits.Invoke(clan);
                }, true);
            }
        }
    }
}
