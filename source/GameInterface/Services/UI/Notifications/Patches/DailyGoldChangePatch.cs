using Common.Messaging;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.UI.Notifications.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.Notifications.Patches;

[HarmonyPatch(typeof(ClanVariablesCampaignBehavior))]
internal class DailyGoldChangePatch
{
    [HarmonyPatch(nameof(ClanVariablesCampaignBehavior.DailyTickClan))]
    [HarmonyPrefix]
    public static bool DailyTickClanPrefix(ref ClanVariablesCampaignBehavior __instance, Clan clan)
    {
        // Only need to notify of daily gold change for player clans
        if (clan != null && !clan.IsPlayerClan()) return true;

        int goldChange = MathF.Round(Campaign.Current.Models.ClanFinanceModel.CalculateClanGoldChange(clan, false, true, false).ResultNumber);

        var message = new NotifyDailyGoldChange(clan, goldChange);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
