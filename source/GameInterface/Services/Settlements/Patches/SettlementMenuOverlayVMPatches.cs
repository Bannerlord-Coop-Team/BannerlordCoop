using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Overlay;

namespace GameInterface.Services.Settlements.Patches;

[HarmonyPatch(typeof(SettlementMenuOverlayVM))]
internal class SettlementMenuOverlayVMPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<SettlementMenuOverlayVMPatches>();

    [HarmonyPatch(nameof(SettlementMenuOverlayVM.OnAssignMembersToSettlement))]
    [HarmonyPrefix]
    public static bool OnAssignMembersToSettlementPrefix(ref SettlementMenuOverlayVM __instance, List<object> leftMembers, Action closePopup)
    {
        Settlement settlement = (MobileParty.MainParty.CurrentSettlement != null) ? MobileParty.MainParty.CurrentSettlement : MobileParty.MainParty.LastVisitedSettlement;
        if (closePopup != null)
        {
            closePopup();
        }

        // Not sure if this is safe. Are not all objects in leftMembers heroes?
        List<Hero> leftHeroes = leftMembers.Cast<Hero>().ToList();

        // Let server handle moving the left heroes from a member roster to the settlement
        var message = new ClanMembersAssignedToSettlement(settlement, MobileParty.MainParty, leftHeroes);
        MessageBroker.Instance.Publish(__instance, message);

        if (leftMembers.Count > 0)
        {
            __instance.InitLists();
        }

        return false;
    }
}
