using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.BesiegerCamps.Patches;

/// <summary>
/// Vanilla's AI assault roll derefs MobileParty.MainParty, which is null on the dedicated host, so
/// the server's own siege AI would crash the first time it weighed an assault. Same math with the
/// player-inside discount derived from player-led parties instead of the main party.
/// </summary>
[HarmonyPatch(typeof(BesiegerCamp))]
internal class BesiegerCampAssaultPatches
{
    [HarmonyPatch(nameof(BesiegerCamp.StartingAssaultOnBesiegedSettlementIsLogical))]
    [HarmonyPrefix]
    private static bool StartingAssaultIsLogicalPrefix(BesiegerCamp __instance, ref bool __result)
    {
        var siegeEvent = __instance.SiegeEvent;
        var settlement = siegeEvent.BesiegedSettlement;

        // The player-inside discount is a constant factor, so one pass collects both.
        bool playerDefenderInside = false;
        float defenderStrength = 0f;
        foreach (var party in settlement.GetInvolvedPartiesForEventType())
        {
            if (!party.IsMobile || party.MobileParty.CurrentSettlement != settlement) continue;

            if (party.LeaderHero != null && party.LeaderHero.IsPlayerHero())
            {
                playerDefenderInside = true;
            }

            if (party.MobileParty.Aggressiveness > 0.01f || party.MobileParty.IsMilitia || party.MobileParty.IsGarrison)
            {
                defenderStrength += party.CalculateCurrentStrength();
            }
        }

        defenderStrength *= playerDefenderInside ? 0.5f : 1f;

        float attackerStrength = 0f;
        foreach (var party in siegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType())
        {
            attackerStrength += party.CalculateCurrentStrength();
        }

        bool hasRam = false;
        bool hasSiegeTower = false;
        foreach (var engine in siegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker).SiegeEngines.AllSiegeEngines())
        {
            if (!engine.IsConstructed) continue;

            if (engine.SiegeEngine == DefaultSiegeEngineTypes.Ram || engine.SiegeEngine == DefaultSiegeEngineTypes.ImprovedRam)
            {
                hasRam = true;
            }
            else if (engine.SiegeEngine == DefaultSiegeEngineTypes.SiegeTower)
            {
                hasSiegeTower = true;
            }
        }

        float settlementAdvantage = Campaign.Current.Models.CombatSimulationModel.GetSettlementAdvantage(settlement);
        float graceHours = (float)CampaignTime.HoursInDay * 4f;
        float advantageExponent = 0.8f - ((siegeEvent.SiegeStartTime.ElapsedHoursUntilNow > graceHours)
            ? ((siegeEvent.SiegeStartTime.ElapsedHoursUntilNow - graceHours) * 0.02f)
            : 0f);
        if (!hasRam) advantageExponent *= 1.25f;
        if (!hasSiegeTower) advantageExponent *= 1.25f;

        float powerRatio = attackerStrength / (defenderStrength * MathF.Pow(settlementAdvantage, advantageExponent));
        __result = false;
        if (powerRatio > 1f)
        {
            int equipmentsBuilt = Campaign.Current.Models.CombatSimulationModel.GetNumberOfEquipmentsBuilt(settlement);
            powerRatio *= (float)MathF.Min(3, equipmentsBuilt) / 3f;
            float equipmentProgress = Campaign.Current.Models.CombatSimulationModel.GetMaximumSiegeEquipmentProgress(settlement) + 0.25f * (float)(5 - equipmentsBuilt);
            powerRatio *= 1f - 0.85f * (equipmentProgress * equipmentProgress);
            float assaultChance = powerRatio * 0.1f;
            __result = defenderStrength == 0f || MBRandom.RandomFloat < assaultChance;
        }

        return false;
    }
}
