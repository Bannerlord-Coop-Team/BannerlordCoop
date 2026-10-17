using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Quests.ArtisanProducts;

internal interface IArtisanProductsTroopReturns
{
    bool TryDefer(IssueBase issue);
    void ReturnPending();
}

internal sealed class ArtisanProductsTroopReturns : IArtisanProductsTroopReturns
{
    private readonly IIssueOwnerResolver owners;
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;
    private readonly Dictionary<string, TroopRoster> pending = new Dictionary<string, TroopRoster>();

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
        foreach (var entry in pending.ToArray())
        {
            if (!players.TryGetPlayer(entry.Key, out var player) || !players.IsConnected(player) ||
                !objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) || hero.PartyBelongedTo == null ||
                !Campaign.Current.Models.IssueModel.CanTroopsReturnFromAlternativeSolution()) continue;
            using (new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
            {
                foreach (var element in entry.Value.GetTroopRoster())
                    if (element.Character.IsHero) element.Character.HeroObject.ChangeState(Hero.CharacterStates.Active);
                hero.PartyBelongedTo.MemberRoster.Add(entry.Value);
                pending.Remove(entry.Key);
            }
        }
    }
}
