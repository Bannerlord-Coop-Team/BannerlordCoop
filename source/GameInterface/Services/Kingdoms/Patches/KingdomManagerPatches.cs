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
        if (rulingClan == Clan.PlayerClan)
        {
            kingdom.Banner = new Banner(Clan.PlayerClan.Banner);
        }
        // v1.5.4 hands the throne to the most influential clan that may rule, if any.
        float num = float.MinValue;
        Clan clan = null;
        foreach (Clan clan2 in kingdom.Clans)
        {
            if (clan2 != rulingClan && Campaign.Current.Models.DiplomacyModel.IsClanEligibleToBecomeRuler(clan2) && clan2.Influence > num)
            {
                num = clan2.Influence;
                clan = clan2;
            }
        }
        if (clan != null)
        {
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
