using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace Coop.Core.Server.Services.MobileParties;

/// <summary>Captures bounded regular-troop XP for the player behind a joining peer.</summary>
public interface IPlayerPartyTroopXpBaselineProvider
{
    bool TryCapture(NetPeer peer, out TroopRosterXpBaseline[] baselines);
}

internal sealed class PlayerPartyTroopXpBaselineProvider : IPlayerPartyTroopXpBaselineProvider
{
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;

    public PlayerPartyTroopXpBaselineProvider(
        IObjectManager objectManager,
        IPlayerManager playerManager)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
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

        baselines = new[] { members, prisoners };
        return true;
    }

    private bool TryCapture(TroopRoster roster, out TroopRosterXpBaseline baseline)
    {
        baseline = default;
        if (!objectManager.TryGetId(roster, out var rosterId)) return false;

        var entries = new List<TroopXpBaselineEntry>();
        foreach (var element in roster.GetTroopRoster())
        {
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
