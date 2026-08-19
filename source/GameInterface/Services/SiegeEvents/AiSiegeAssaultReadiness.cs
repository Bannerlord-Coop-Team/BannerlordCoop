using GameInterface.Services.Heroes.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.SiegeEvents;

public readonly struct AiSiegeAssaultReadinessInput
{
    public float AttackerStrength { get; }
    public float DefenderStrength { get; }
    public float SettlementAdvantage { get; }
    public float SiegeElapsedHours { get; }
    public bool HasRam { get; }
    public bool HasSiegeTower { get; }
    public int EquipmentBuilt { get; }
    public float MaximumEquipmentProgress { get; }

    public AiSiegeAssaultReadinessInput(
        float attackerStrength,
        float defenderStrength,
        float settlementAdvantage,
        float siegeElapsedHours,
        bool hasRam,
        bool hasSiegeTower,
        int equipmentBuilt,
        float maximumEquipmentProgress)
    {
        AttackerStrength = attackerStrength;
        DefenderStrength = defenderStrength;
        SettlementAdvantage = settlementAdvantage;
        SiegeElapsedHours = siegeElapsedHours;
        HasRam = hasRam;
        HasSiegeTower = hasSiegeTower;
        EquipmentBuilt = equipmentBuilt;
        MaximumEquipmentProgress = maximumEquipmentProgress;
    }
}

public readonly struct AiSiegeAssaultReadinessResult
{
    public float AttackerStrength { get; }
    public float DefenderStrength { get; }
    public float PowerRatioBeforeEquipment { get; }
    public float PowerRatioAfterEquipment { get; }
    public float AssaultChance { get; }

    public bool IsViable => DefenderStrength == 0f ||
        (PowerRatioBeforeEquipment > 1f && AssaultChance > 0f);

    public AiSiegeAssaultReadinessResult(
        float attackerStrength,
        float defenderStrength,
        float powerRatioBeforeEquipment,
        float powerRatioAfterEquipment,
        float assaultChance)
    {
        AttackerStrength = attackerStrength;
        DefenderStrength = defenderStrength;
        PowerRatioBeforeEquipment = powerRatioBeforeEquipment;
        PowerRatioAfterEquipment = powerRatioAfterEquipment;
        AssaultChance = assaultChance;
    }
}

public interface IAiSiegeAssaultReadiness
{
    AiSiegeAssaultReadinessResult Evaluate(BesiegerCamp camp);
    AiSiegeAssaultReadinessResult Evaluate(AiSiegeAssaultReadinessInput input);
    bool ShouldStartAssault(BesiegerCamp camp);
}

/// <summary>Evaluates the inputs used by vanilla's AI siege assault roll.</summary>
internal class AiSiegeAssaultReadiness : IAiSiegeAssaultReadiness
{
    public AiSiegeAssaultReadinessResult Evaluate(BesiegerCamp camp)
    {
        var siegeEvent = camp.SiegeEvent;
        var settlement = siegeEvent.BesiegedSettlement;

        bool playerDefenderInside = false;
        float defenderStrength = 0f;
        foreach (var party in settlement.GetInvolvedPartiesForEventType())
        {
            if (!party.IsMobile || party.MobileParty.CurrentSettlement != settlement) continue;

            if (party.LeaderHero?.IsPlayerHero() == true)
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
        foreach (var party in camp.GetInvolvedPartiesForEventType())
        {
            attackerStrength += party.CalculateCurrentStrength();
        }

        bool hasRam = false;
        bool hasSiegeTower = false;
        foreach (var engine in camp.SiegeEngines.AllSiegeEngines())
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

        return Evaluate(new AiSiegeAssaultReadinessInput(
            attackerStrength,
            defenderStrength,
            Campaign.Current.Models.CombatSimulationModel.GetSettlementAdvantage(settlement),
            siegeEvent.SiegeStartTime.ElapsedHoursUntilNow,
            hasRam,
            hasSiegeTower,
            Campaign.Current.Models.CombatSimulationModel.GetNumberOfEquipmentsBuilt(settlement),
            Campaign.Current.Models.CombatSimulationModel.GetMaximumSiegeEquipmentProgress(settlement)));
    }

    public AiSiegeAssaultReadinessResult Evaluate(AiSiegeAssaultReadinessInput input)
    {
        float graceHours = (float)CampaignTime.HoursInDay * 4f;
        float advantageExponent = 0.8f - ((input.SiegeElapsedHours > graceHours)
            ? ((input.SiegeElapsedHours - graceHours) * 0.02f)
            : 0f);
        if (!input.HasRam) advantageExponent *= 1.25f;
        if (!input.HasSiegeTower) advantageExponent *= 1.25f;

        float powerRatioBeforeEquipment = input.AttackerStrength /
            (input.DefenderStrength * MathF.Pow(input.SettlementAdvantage, advantageExponent));
        float powerRatioAfterEquipment = powerRatioBeforeEquipment;
        if (powerRatioBeforeEquipment > 1f)
        {
            powerRatioAfterEquipment *= (float)MathF.Min(3, input.EquipmentBuilt) / 3f;
            float equipmentProgress = input.MaximumEquipmentProgress + (0.25f * (float)(5 - input.EquipmentBuilt));
            powerRatioAfterEquipment *= 1f - (0.85f * (equipmentProgress * equipmentProgress));
        }

        return new AiSiegeAssaultReadinessResult(
            input.AttackerStrength,
            input.DefenderStrength,
            powerRatioBeforeEquipment,
            powerRatioAfterEquipment,
            powerRatioAfterEquipment * 0.1f);
    }

    public bool ShouldStartAssault(BesiegerCamp camp)
    {
        var result = Evaluate(camp);
        if (result.PowerRatioBeforeEquipment <= 1f) return false;

        return result.DefenderStrength == 0f || MBRandom.RandomFloat < result.AssaultChance;
    }
}
