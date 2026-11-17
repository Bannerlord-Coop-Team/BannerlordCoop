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
        Clan clan = FindSuccessorClan(kingdom);
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

    // The throne goes to the most influential other clan that may rule, or null when none may.
    internal static Clan FindSuccessorClan(Kingdom kingdom)
    {
        Clan successor = null;
        float highestInfluence = float.MinValue;
        foreach (Clan candidate in kingdom.Clans)
        {
            if (candidate == kingdom.RulingClan) continue;
            if (!Campaign.Current.Models.DiplomacyModel.IsClanEligibleToBecomeRuler(candidate)) continue;
            if (candidate.Influence <= highestInfluence) continue;

            highestInfluence = candidate.Influence;
            successor = candidate;
        }
        return successor;
    }
}
