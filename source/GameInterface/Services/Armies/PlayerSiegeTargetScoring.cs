using GameInterface.Services.Players;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Armies;

public interface IPlayerSiegeTargetScoring
{
    SettlementDefenseScore CalculateSettlementDefense(Settlement targetSettlement);
}

public class PlayerSiegeTargetScoring : IPlayerSiegeTargetScoring
{
    private const float PlayerPartyWeight = 0.5f;
    private const float PlayerLedArmyPartyWeight = 0.8f;
    private const float PlayerPresenceWeight = 0.8f;

    private readonly IPlayerManager playerManager;

    public PlayerSiegeTargetScoring(IPlayerManager playerManager)
    {
        if (playerManager == null) throw new ArgumentNullException(nameof(playerManager));

        this.playerManager = playerManager;
    }

    public SettlementDefenseScore CalculateSettlementDefense(Settlement targetSettlement)
    {
        if (targetSettlement == null) throw new ArgumentNullException(nameof(targetSettlement));

        var accumulator = new SettlementDefenseAccumulator();
        foreach (MobileParty party in targetSettlement.Parties)
        {
            bool isPlayerParty = playerManager.Contains(party);
            bool isLedByPlayerParty = !isPlayerParty &&
                                      party.Army?.LeaderParty != null &&
                                      playerManager.Contains(party.Army.LeaderParty);
            bool isEligible = party.Aggressiveness > 0.01f || party.IsGarrison || party.IsMilitia;
            bool countsAsMobileLord = !party.IsGarrison && !party.IsMilitia && party.LeaderHero != null;

            accumulator.Add(new SettlementDefenderScoreData(
                party.Party.EstimatedStrength,
                isEligible,
                countsAsMobileLord,
                isPlayerParty,
                isLedByPlayerParty));
        }

        return accumulator.GetScore();
    }

    internal SettlementDefenseScore CalculateSettlementDefense(
        IReadOnlyCollection<SettlementDefenderScoreData> defenders)
    {
        if (defenders == null) throw new ArgumentNullException(nameof(defenders));

        var accumulator = new SettlementDefenseAccumulator();
        foreach (SettlementDefenderScoreData defender in defenders)
            accumulator.Add(defender);

        return accumulator.GetScore();
    }

    private struct SettlementDefenseAccumulator
    {
        private bool hasPlayerAtSettlement;
        private float totalStrength;
        private float mobileLordStrength;

        public void Add(SettlementDefenderScoreData defender)
        {
            if (defender.IsPlayerParty)
                hasPlayerAtSettlement = true;

            if (!defender.IsEligible)
                return;

            float partyWeight = defender.IsPlayerParty
                ? PlayerPartyWeight
                : defender.IsLedByPlayerParty
                    ? PlayerLedArmyPartyWeight
                    : 1f;
            float weightedStrength = defender.Strength * partyWeight;

            totalStrength += weightedStrength;
            if (defender.CountsAsMobileLord)
                mobileLordStrength += weightedStrength;
        }

        public SettlementDefenseScore GetScore()
        {
            float settlementWeight = hasPlayerAtSettlement ? PlayerPresenceWeight : 1f;
            return new SettlementDefenseScore(
                totalStrength * settlementWeight,
                mobileLordStrength * settlementWeight);
        }
    }
}

public readonly struct SettlementDefenseScore
{
    public float TotalStrength { get; }
    public float MobileLordStrength { get; }

    public SettlementDefenseScore(float totalStrength, float mobileLordStrength)
    {
        TotalStrength = totalStrength;
        MobileLordStrength = mobileLordStrength;
    }
}

internal readonly struct SettlementDefenderScoreData
{
    public float Strength { get; }
    public bool IsEligible { get; }
    public bool CountsAsMobileLord { get; }
    public bool IsPlayerParty { get; }
    public bool IsLedByPlayerParty { get; }

    public SettlementDefenderScoreData(
        float strength,
        bool isEligible,
        bool countsAsMobileLord,
        bool isPlayerParty,
        bool isLedByPlayerParty)
    {
        Strength = strength;
        IsEligible = isEligible;
        CountsAsMobileLord = countsAsMobileLord;
        IsPlayerParty = isPlayerParty;
        IsLedByPlayerParty = isLedByPlayerParty;
    }
}
