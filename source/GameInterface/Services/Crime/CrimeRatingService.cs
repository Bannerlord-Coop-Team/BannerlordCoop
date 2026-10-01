using Common;
using Common.Network;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Crime;

internal interface ICrimeRatingService
{
    float Get(IFaction faction);
    void Set(IFaction faction, float value);
    void Apply(Hero hero, IFaction faction, float delta, bool showNotification);
    void Request(IFaction faction, float delta, bool showNotification);
    void Notify(IFaction faction, float previousRating);
    void DailyTick();
    void MakePeace(IFaction first, IFaction second);
    void HeroDied(Hero hero);
}

internal class CrimeRatingService : ICrimeRatingService
{
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;
    private readonly INetwork network;

    public CrimeRatingService(IPlayerManager players, IObjectManager objects, INetwork network)
    {
        this.players = players;
        this.objects = objects;
        this.network = network;
    }

    private Hero CurrentHero => ModInformation.IsServer ? ResolvedMainHeroContext.ResolvedMainHero : Hero.MainHero;

    private bool TryGetPlayer(Hero hero, out Player player)
    {
        player = null;
        return hero != null && PlayerManager.TryGetControlledObjectInfo(hero, out var control)
            && players.TryGetPlayer(control.ObjectControllerId, out player);
    }

    public float Get(IFaction faction)
    {
        return TryGetPlayer(CurrentHero, out var player) && objects.TryGetId(faction, out var id)
            && player.CrimeRatings.TryGetValue(id, out var rating) ? rating : 0f;
    }

    public void Set(IFaction faction, float value)
    {
        if (!ModInformation.IsServer || float.IsNaN(value) || float.IsInfinity(value)) return;
        if (!TryGetPlayer(CurrentHero, out var player) || !objects.TryGetIdWithLogging(faction, out var id)) return;
        player.CrimeRatings[id] = value;
        network.SendAll(new NetworkCrimeRatingChanged(player.ControllerId, player.HeroId, id, value));
    }

    public void Apply(Hero hero, IFaction faction, float delta, bool showNotification)
    {
        if (!ModInformation.IsServer || faction == null || !TryGetPlayer(hero, out _)
            || float.IsNaN(delta) || float.IsInfinity(delta)) return;
        using (new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
            ChangeCrimeRatingAction.Apply(faction, delta, showNotification);
    }

    public void Notify(IFaction faction, float previousRating)
    {
        if (!ModInformation.IsServer || !TryGetPlayer(CurrentHero, out var player)
            || !objects.TryGetIdWithLogging(faction, out var id)) return;
        var rating = Get(faction);
        network.SendAll(new NetworkCrimeRatingNotification(player.ControllerId, player.HeroId, id, rating, rating - previousRating));
    }

    public void Request(IFaction faction, float delta, bool showNotification)
    {
        if (!objects.TryGetIdWithLogging(faction, out var id)) return;
        network.SendAll(new RequestCrimeRatingChange(id, delta, showNotification));
    }

    private IEnumerable<IFaction> Factions => Clan.NonBanditFactions.Cast<IFaction>()
        .Concat(Kingdom.All).Where(faction => !faction.IsEliminated).ToArray();

    private void ForEachPlayer(Action<Hero> action)
    {
        foreach (var player in players.Players)
        {
            if (!objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) || hero.IsDead) continue;
            using (new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
                action(hero);
        }
    }

    public void DailyTick()
    {
        ForEachPlayer(hero =>
        {
            foreach (var faction in Factions)
            {
                var delta = Campaign.Current.Models.CrimeModel.GetDailyCrimeRatingChange(faction).ResultNumber;
                if (delta != 0f) Apply(hero, faction, delta, false);
            }
        });
    }

    public void MakePeace(IFaction first, IFaction second)
    {
        ForEachPlayer(hero =>
        {
            var faction = hero.MapFaction == first ? second : hero.MapFaction == second ? first : null;
            if (faction == null) return;
            var limit = Campaign.Current.Models.CrimeModel.DeclareWarCrimeRatingThreshold * 0.5f;
            var rating = Get(faction);
            if (rating > limit) Apply(hero, faction, limit - rating, false);
        });
    }

    public void HeroDied(Hero hero)
    {
        if (!TryGetPlayer(hero, out _)) return;
        using (new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
            foreach (var faction in Factions) Set(faction, 0f);
    }
}
