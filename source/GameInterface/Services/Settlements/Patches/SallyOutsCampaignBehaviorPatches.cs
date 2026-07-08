using Common;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// Server-side gate for the automatic garrison sally-out check. Vanilla's guard derefs Hero.MainHero,
/// which is null on the dedicated host, so the guard is reimplemented with "a player hero leads the
/// defense from inside" keeping the manual sally-out call with player defenders.
/// </summary>
[HarmonyPatch(typeof(SallyOutsCampaignBehavior))]
internal class SallyOutsCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(SallyOutsCampaignBehavior.CheckForSettlementSallyOut))]
    [HarmonyPrefix]
    private static bool CheckForSettlementSallyOutPrefix(SallyOutsCampaignBehavior __instance, Settlement settlement)
    {
        if (ModInformation.IsClient) return false;

        if (!settlement.IsFortification || settlement.SiegeEvent == null || settlement.Party.MapEvent != null) return false;
        if (settlement.Town.GarrisonParty == null || settlement.Town.GarrisonParty.MapEvent != null) return false;

        var besiegerMapEvent = settlement.SiegeEvent.BesiegerCamp.LeaderParty?.MapEvent;
        bool besiegerBusyOutside = besiegerMapEvent != null && (besiegerMapEvent.IsSiegeOutside || besiegerMapEvent.IsBlockade);
        if (!besiegerBusyOutside && MathF.Floor(CampaignTime.Now.ToHours) % 4 != 0) return false;

        var defenseLeader = Campaign.Current.Models.EncounterModel.GetLeaderOfSiegeEvent(settlement.SiegeEvent, BattleSideEnum.Defender);
        if (defenseLeader != null && defenseLeader.IsPlayerHero() && defenseLeader.CurrentSettlement == settlement) return false;

        __instance.CheckSallyOut(settlement, checkForNavalSallyOut: false, out var salliedOut);

        if (!salliedOut && settlement.HasPort && settlement.SiegeEvent.IsBlockadeActive)
        {
            __instance.CheckSallyOut(settlement, checkForNavalSallyOut: true, out _);
        }

        return false;
    }
}
