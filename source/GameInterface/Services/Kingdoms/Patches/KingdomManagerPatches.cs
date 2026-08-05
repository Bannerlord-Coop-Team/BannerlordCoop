using Common;
using Common.Messaging;
using GameInterface.Services.Kingdoms.Messages;
using HarmonyLib;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch]
internal class KingdomManagerPatches
{
    [HarmonyPatch(typeof(KingdomManager), nameof(KingdomManager.AbdicateTheThrone))]
    [HarmonyPrefix]
    private static bool Prefix(KingdomManager __instance, Kingdom kingdom)
    {
        Clan rulingClan = kingdom.RulingClan;
        int num = kingdom.Clans.Count((Clan x) => !x.IsUnderMercenaryService);
        if (rulingClan == Clan.PlayerClan)
        {
            kingdom.Banner = new Banner(Clan.PlayerClan.Banner);
        }
        if (num > 1)
        {
            float num2 = float.MinValue;
            Clan clan = null;
            foreach (Clan clan2 in kingdom.Clans)
            {
                if (clan2 != rulingClan && clan2.Influence > num2)
                {
                    num2 = clan2.Influence;
                    clan = clan2;
                }
            }
            MessageBroker.Instance.Publish(__instance, new RulingClanChanged(kingdom, clan));
            GameThread.WaitWhilePumping(() => kingdom.RulingClan == clan, DateTime.UtcNow.AddSeconds(5));
            kingdom.AddDecision(new KingSelectionKingdomDecision(rulingClan, rulingClan)
            {
                IsEnforced = true
            }, true);
            return false;
        }
        MessageBroker.Instance.Publish(__instance, new DestroyKingdom(kingdom));
        return false;
    }
}
