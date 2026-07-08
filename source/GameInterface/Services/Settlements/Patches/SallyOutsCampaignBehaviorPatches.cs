using Common;
using Common.Logging;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using Serilog;
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
    private static readonly ILogger Logger = LogManager.GetLogger<SallyOutsCampaignBehaviorPatches>();

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

        // TEMP [SallyDiag]: confirmed the sally scan skips the besieger because its CurrentSettlement is non-null
        // (it reads as zero strength, so the garrison out-ratios it every check). Logs CurrentSettlement + behavior
        // + outcome to verify the leave-on-siege-start fix holds and the party isn't re-entering the settlement.
        var besieger = settlement.SiegeEvent.BesiegerCamp.LeaderParty;
        Logger.Information("[SallyDiag] {Settlement}: besieger={Besieger} count={Count} strength={Strength} aggr={Aggr} curSettle={CurSettle} behavior={Behavior} atWar={AtWar} garrison={Garrison} -> salliedOut={Sallied}",
            settlement.Name?.ToString(), besieger?.Name?.ToString(), besieger?.MemberRoster.TotalManCount, besieger?.Party.EstimatedStrength,
            besieger?.Aggressiveness, besieger?.CurrentSettlement?.Name?.ToString(), besieger?.DefaultBehavior, besieger?.MapFaction?.IsAtWarWith(settlement.MapFaction),
            settlement.Town.GarrisonParty?.MemberRoster.TotalManCount, salliedOut);

        if (!salliedOut && settlement.HasPort && settlement.SiegeEvent.IsBlockadeActive)
        {
            __instance.CheckSallyOut(settlement, checkForNavalSallyOut: true, out _);
        }

        return false;
    }
}
