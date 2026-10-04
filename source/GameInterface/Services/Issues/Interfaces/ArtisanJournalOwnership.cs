using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Issues.Interfaces;

public interface IArtisanJournalOwnership
{
    void Record(MBObjectBase subject, Hero giver);
    bool IsVisible(JournalLogEntry entry);
    void SyncData(IDataStore dataStore);
}

internal sealed class ArtisanJournalOwnership : IArtisanJournalOwnership
{
    private readonly Dictionary<string, string> owners = new();
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IControllerIdProvider controller;
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;

    public ArtisanJournalOwnership(IIssueOwnershipRegistry ownership, IControllerIdProvider controller,
        IPlayerManager players, IObjectManager objects)
    {
        this.ownership = ownership;
        this.controller = controller;
        this.players = players;
        this.objects = objects;
    }

    public void Record(MBObjectBase subject, Hero giver)
    {
        if (subject?.StringId == null || owners.ContainsKey(subject.StringId)) return;
        ownership.TryGetOwnerControllerId(giver, out var owner);
        if (owner == null && (QuestSolutionStartAuthorityGuard.IsActive || AlternativeSolutionStartAuthorityGuard.IsActive ||
            ArtisanOverpricedGoodsActionGuard.IsActiveFor(giver)) && Hero.MainHero != null &&
            objects.TryGetIdWithLogging(Hero.MainHero, out var heroId))
            owner = players.Players.FirstOrDefault(player => player.HeroId == heroId)?.ControllerId;
        owners[subject.StringId] = owner;
    }

    public bool IsVisible(JournalLogEntry entry)
    {
        if (entry == null) return true;
        foreach (var id in entry._relatedObjectIds)
            if (owners.TryGetValue(id, out var owner)) return owner != null && owner == controller.ControllerId;
        return true;
    }

    public void SyncData(IDataStore dataStore)
    {
        var entries = owners.ToArray();
        string[] ids = dataStore.IsSaving ? entries.Select(entry => entry.Key).ToArray() : null;
        string[] controllers = dataStore.IsSaving ? entries.Select(entry => entry.Value).ToArray() : null;
        dataStore.SyncData("_coop_artisan_journal_ids", ref ids);
        dataStore.SyncData("_coop_artisan_journal_controllers", ref controllers);
        if (!dataStore.IsLoading) return;
        owners.Clear();
        if (ids == null || controllers == null || ids.Length != controllers.Length) return;
        for (var i = 0; i < ids.Length; i++)
            if (ids[i] != null) owners[ids[i]] = controllers[i];
    }
}
