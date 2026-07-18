using Common;
using Common.Messaging;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.UI.Notifications.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.UI.Notifications.Patches;

[HarmonyPatch(typeof(ClanVariablesCampaignBehavior))]
internal class DailyGoldChangePatch
{
    [HarmonyPatch(nameof(ClanVariablesCampaignBehavior.DailyTickClan))]
    [HarmonyPrefix]
    public static void DailyTickClanPrefix(Clan clan, out int? __state)
    {
        __state = null;
        if (ModInformation.IsServer && clan != null && clan.IsPlayerClan())
        {
            __state = clan.Gold;
        }
    }

    [HarmonyPatch(nameof(ClanVariablesCampaignBehavior.DailyTickClan))]
    [HarmonyPostfix]
    public static void DailyTickClanPostfix(ClanVariablesCampaignBehavior __instance, Clan clan, int? __state)
    {
        if (!__state.HasValue) return;

        int goldChange = clan.Gold - __state.Value;
        var message = new NotifyDailyGoldChange(clan, goldChange);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
