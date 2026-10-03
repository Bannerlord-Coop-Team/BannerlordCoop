using Common;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Interfaces;

internal interface IArtisanProductGenerationContext
{
    void Request(string controllerId);
    void Clear();
    bool TryEnter(out IDisposable scope);
}

internal sealed class ArtisanProductGenerationContext : IArtisanProductGenerationContext
{
    private readonly HashSet<string> requesters = new();
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;

    public ArtisanProductGenerationContext(IPlayerManager players, IObjectManager objects)
    {
        this.players = players;
        this.objects = objects;
    }

    public void Request(string controllerId) => requesters.Add(controllerId);

    public void Clear() => requesters.Clear();

    public bool TryEnter(out IDisposable scope)
    {
        scope = null;
        if (ModInformation.IsClient) return false;
        foreach (var player in players.Players.OrderBy(player => player.ControllerId, StringComparer.Ordinal))
        {
            if (!requesters.Contains(player.ControllerId) || !players.IsConnected(player)) continue;
            if (!objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) continue;
            if (!objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) continue;
            scope = new MainHeroSubstitutionScope(hero, party);
            return true;
        }
        return false;
    }
}
