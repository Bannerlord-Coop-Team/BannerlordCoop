using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Interfaces;

public interface IHeadmanHerdGenerationContext
{
    bool TryEnter(Hero giver, out IDisposable scope);
}

internal sealed class HeadmanHerdGenerationContext : IHeadmanHerdGenerationContext
{
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;

    public HeadmanHerdGenerationContext(IPlayerManager players, IObjectManager objects)
    {
        this.players = players;
        this.objects = objects;
    }

    public bool TryEnter(Hero giver, out IDisposable scope)
    {
        scope = null;
        if (giver?.CurrentSettlement == null) return false;
        Hero selectedHero = null;
        MobileParty selectedParty = null;
        string selectedController = null;
        var closestDistance = float.MaxValue;
        foreach (var player in players.Players)
        {
            if (!players.IsConnected(player)) continue;
            if (!objects.TryGetObject<Hero>(player.HeroId, out var hero)) continue;
            if (!objects.TryGetObject<MobileParty>(player.MobilePartyId, out var party)) continue;
            if (!hero.IsActive || !party.IsActive) continue;
            var distance = party.Position.DistanceSquared(giver.CurrentSettlement.Position);
            if (distance > closestDistance || float.IsNaN(distance)) continue;
            if (distance == closestDistance && string.CompareOrdinal(player.ControllerId, selectedController) >= 0) continue;
            closestDistance = distance;
            selectedHero = hero;
            selectedParty = party;
            selectedController = player.ControllerId;
        }
        if (selectedHero == null) return false;
        // Generation needs a real navigation capability, but does not assign quest ownership.
        scope = new MainHeroSubstitutionScope(selectedHero, selectedParty);
        return true;
    }
}
