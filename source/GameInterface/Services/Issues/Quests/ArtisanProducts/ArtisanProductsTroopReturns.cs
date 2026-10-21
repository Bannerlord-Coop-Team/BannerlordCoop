using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Issues.Quests.ArtisanProducts;

internal interface IArtisanProductsTroopReturns
{
    bool TryDefer(IssueBase issue);
    void ReturnPending();
    void SyncData(IDataStore dataStore);
}

internal sealed class ArtisanProductsTroopReturns : IArtisanProductsTroopReturns
{
    private readonly IIssueOwnerResolver owners;
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;
    private readonly Dictionary<string, TroopRoster> pending = new Dictionary<string, TroopRoster>();
    private static List<ArtisanProductsPendingTroopSaveData> savedPending;

    public ArtisanProductsTroopReturns(IIssueOwnerResolver owners, IPlayerManager players, IObjectManager objects)
    { this.owners = owners; this.players = players; this.objects = objects; }

    public bool TryDefer(IssueBase issue)
    {
        if (Campaign.Current.Models.IssueModel.CanTroopsReturnFromAlternativeSolution() ||
            !owners.TryGetOwnerControllerId(issue, out var controller)) return false;
        if (!pending.TryGetValue(controller, out var roster))
        { roster = TroopRoster.CreateDummyTroopRoster(); pending.Add(controller, roster); }
        roster.Add(issue.AlternativeSolutionSentTroops);
        return true;
    }

    public void ReturnPending()
    {
        RehydratePending();
        foreach (var entry in pending.ToArray())
        {
            if (!players.TryGetPlayer(entry.Key, out var player) || !players.IsConnected(player) ||
                !objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) || hero.PartyBelongedTo == null) continue;
            using (new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
            {
                if (!Campaign.Current.Models.IssueModel.CanTroopsReturnFromAlternativeSolution()) continue;
                foreach (var element in entry.Value.GetTroopRoster())
                    if (element.Character.IsHero) element.Character.HeroObject.ChangeState(Hero.CharacterStates.Active);
                hero.PartyBelongedTo.MemberRoster.Add(entry.Value);
                pending.Remove(entry.Key);
            }
        }
    }

    public void SyncData(IDataStore dataStore)
    {
        List<ArtisanProductsPendingTroopSaveData> data = null;
        if (dataStore.IsSaving)
        {
            data = pending.SelectMany(entry => entry.Value.GetTroopRoster().Select(element =>
                new ArtisanProductsPendingTroopSaveData(entry.Key, element.Character, element.Number, element.WoundedNumber, element.Xp))).ToList();
        }

        dataStore.SyncData("_coop_artisan_products_pending_troops", ref data);
        if (dataStore.IsLoading)
        {
            savedPending = data;
            pending.Clear();
        }
    }

    private void RehydratePending()
    {
        if (savedPending == null) return;
        foreach (var entry in savedPending)
        {
            if (entry?.Character == null || string.IsNullOrEmpty(entry.ControllerId)) continue;
            if (!pending.TryGetValue(entry.ControllerId, out var roster))
            { roster = TroopRoster.CreateDummyTroopRoster(); pending.Add(entry.ControllerId, roster); }
            roster.AddToCounts(entry.Character, entry.Number, entry.WoundedNumber, entry.Xp, false);
        }
        savedPending = null;
    }
}

internal sealed class ArtisanProductsPendingTroopSaveData
{
    [SaveableField(1)] internal string ControllerId;
    [SaveableField(2)] internal CharacterObject Character;
    [SaveableField(3)] internal int Number;
    [SaveableField(4)] internal int WoundedNumber;
    [SaveableField(5)] internal int Xp;

    private ArtisanProductsPendingTroopSaveData() { }

    internal ArtisanProductsPendingTroopSaveData(string controllerId, CharacterObject character, int number, int woundedNumber, int xp)
    { ControllerId = controllerId; Character = character; Number = number; WoundedNumber = woundedNumber; Xp = xp; }
}

public sealed class ArtisanProductsSaveableTypeDefiner : SaveableTypeDefiner
{
    public ArtisanProductsSaveableTypeDefiner() : base(44_186_004) { }

    public override void DefineClassTypes()
    {
        AddClassDefinition(typeof(ArtisanProductsPendingTroopSaveData), 1);
    }

    public override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<ArtisanProductsPendingTroopSaveData>));
    }
}
