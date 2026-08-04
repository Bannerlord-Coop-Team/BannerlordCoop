using Common.Messaging;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Kingdoms.Extentions;
using GameInterface.Services.UI.Notifications.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch(typeof(TributesCampaignBehaviour))]
internal class TributesCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(TributesCampaignBehaviour.OnClanEarnedGoldFromTribute))]
    [HarmonyPrefix]
    public static bool OnClanEarnedGoldFromTributePrefix(Clan clan, IFaction payerFaction)
    {
        if (clan == null) return false;

        StanceLink stanceWith = clan.MapFaction.GetStanceWith(payerFaction);
        if ((clan.IsPlayerClan() || payerFaction.IsPlayerFaction()) && stanceWith.GetRemainingTributePaymentCount() == 0)
        {
            var message = new NotifyTributePaymentEnded(clan, payerFaction);
            MessageBroker.Instance.Publish(null, message);
        }

        return false;
    }
}
