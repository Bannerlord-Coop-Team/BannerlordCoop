using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters;
using LiteNetLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace Coop.Core.Server.Services.MobileParties;

/// <summary>Captures bounded regular-troop XP for a joining player and their clan parties.</summary>
public interface IPlayerPartyTroopXpBaselineProvider
{
    bool TryCapture(NetPeer peer, out TroopRosterXpBaseline[] baselines);
}

internal sealed class PlayerPartyTroopXpBaselineProvider : IPlayerPartyTroopXpBaselineProvider
{
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IPlayerTroopXpRelevance playerTroopXpRelevance;

    public PlayerPartyTroopXpBaselineProvider(
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IPlayerTroopXpRelevance playerTroopXpRelevance)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.playerTroopXpRelevance = playerTroopXpRelevance;
    }

    public bool TryCapture(NetPeer peer, out TroopRosterXpBaseline[] baselines)
    {
        baselines = Array.Empty<TroopRosterXpBaseline>();
        if (peer == null ||
            !playerManager.TryGetPlayer(peer, out var player) ||
            !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party) ||
            !TryCapture(party.MemberRoster, out var members) ||
            !TryCapture(party.PrisonRoster, out var prisoners))
        {
            return false;
        }

        var captured = new List<TroopRosterXpBaseline> { members, prisoners };
        foreach (var candidate in Campaign.Current.CampaignObjectManager.MobileParties)
        {
            if (candidate == null || !candidate.IsActive || ReferenceEquals(candidate, party) ||
                !playerTroopXpRelevance.IsRelevant(candidate, player))
            {
                continue;
            }

            if (!TryCapture(candidate.MemberRoster, out var candidateMembers) ||
                !TryCapture(candidate.PrisonRoster, out var candidatePrisoners))
            {
                return false;
            }

            captured.Add(candidateMembers);
            captured.Add(candidatePrisoners);
        }

        baselines = captured.ToArray();
        return true;
    }

    private bool TryCapture(TroopRoster roster, out TroopRosterXpBaseline baseline)
    {
        baseline = default;
        if (!objectManager.TryGetId(roster, out var rosterId)) return false;

        var entries = new List<TroopXpBaselineEntry>();
        for (int index = 0; index < roster.Count; index++)
        {
            TroopRosterElement element = roster.GetElementCopyAtIndex(index);
            CharacterObject character = element.Character;
            if (character == null || character.IsHero) continue;
            if (!objectManager.TryGetId(character, out var characterId)) return false;

            entries.Add(new TroopXpBaselineEntry(
                Compact(characterId, typeof(CharacterObject)),
                element.Xp));
        }

        baseline = new TroopRosterXpBaseline(
            Compact(rosterId, typeof(TroopRoster)),
            entries.ToArray());
        return true;
    }
}
