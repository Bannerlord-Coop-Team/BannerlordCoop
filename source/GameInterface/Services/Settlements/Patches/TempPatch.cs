using Common.Messaging;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using static TaleWorlds.CampaignSystem.Actions.ChangeOwnerOfSettlementAction;

namespace GameInterface.Services.Settlements.Patches
{
    [HarmonyPatch(typeof(Clan))]
    public class TempPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Clan.ChangeClanName))]
        private static bool Prefix(TextObject name, TextObject informalName)
        {
            Settlement settlement = null;
            Settlement.All.ForEach(t => 
            {
                if (t.Name.ToString() == "Danustica") settlement = t;
            });

            Hero hero = MobileParty.All.First(x => x.ActualClan?.Kingdom?.ToString() == "Southern Empire").LeaderHero;

            var message = new LocalSettlementOwnershipChange(settlement.StringId, hero.StringId, hero.StringId, Convert.ToInt32(ChangeOwnerOfSettlementDetail.Default));

            MessageBroker.Instance.Publish(settlement, message);

            return true;
        }
    }
}
