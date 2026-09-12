using Common.Logging;
using GameInterface.Utils;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Bandits;

internal interface IBanditPartyHomeSettlementRepairer
{
    int RepairMissingHomeSettlements(
        IEnumerable<MobileParty> banditParties,
        IEnumerable<Settlement> settlements);
}

internal class BanditPartyHomeSettlementRepairer : IBanditPartyHomeSettlementRepairer
{
    private static readonly ILogger Logger = LogManager.GetLogger<BanditPartyHomeSettlementRepairer>();

    public int RepairMissingHomeSettlements(
        IEnumerable<MobileParty> banditParties,
        IEnumerable<Settlement> settlements)
    {
        var homeSettlements = new List<Settlement>();
        foreach (Settlement settlement in settlements)
        {
            if (settlement.IsTown || settlement.IsVillage)
            {
                homeSettlements.Add(settlement);
            }
        }

        var repairedCount = 0;
        foreach (MobileParty party in banditParties)
        {
            if (!(party.PartyComponent is BanditPartyComponent component) ||
                component.HomeSettlement != null)
            {
                continue;
            }

            Settlement nearestSettlement = FindNearestSettlement(party, homeSettlements);
            if (nearestSettlement == null)
            {
                Logger.Error("Could not repair missing home settlement for bandit party {PartyId}", party.StringId);
                continue;
            }

            // Vanilla exposes no setter for its readonly saveable _relatedSettlement field.
            ReflectionUtils.SetPrivateField(
                typeof(BanditPartyComponent),
                nameof(BanditPartyComponent._relatedSettlement),
                component,
                nearestSettlement);
            repairedCount++;
        }

        if (repairedCount > 0)
        {
            Logger.Warning("Repaired missing home settlements for {Count} bandit parties", repairedCount);
        }

        return repairedCount;
    }

    private static Settlement FindNearestSettlement(
        MobileParty party,
        IEnumerable<Settlement> settlements)
    {
        Settlement nearestSettlement = null;
        float nearestDistanceSquared = float.MaxValue;

        foreach (Settlement settlement in settlements)
        {
            float distanceSquared = settlement.Position.DistanceSquared(party.Position);
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestSettlement = settlement;
                nearestDistanceSquared = distanceSquared;
            }
        }

        return nearestSettlement;
    }
}
