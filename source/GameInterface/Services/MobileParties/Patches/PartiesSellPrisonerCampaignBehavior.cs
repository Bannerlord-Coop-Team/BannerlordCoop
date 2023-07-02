﻿using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(PartiesSellPrisonerCampaignBehavior), "OnSettlementEntered")]
internal class PartiesSellPrisonerCampaignBehaviorPatch
{
    private static bool Prefix(ref MobileParty mobileParty, ref Settlement settlement, ref Hero hero)
    {
        // TODO only allow for server and broadcast when it happens
        return true;
    }
}