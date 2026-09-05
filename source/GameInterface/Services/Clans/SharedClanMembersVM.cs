using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Clans;

public class SharedClanMembersVM : ClanMembersVM
{
    private readonly IClanMemberGrouping grouping;

    [DataSourceProperty]
    public MBBindingList<ClanLordItemVM> Players { get; } = new();

    [DataSourceProperty]
    public MBBindingList<ClanLordItemVM> OtherFamilies { get; } = new();

    [DataSourceProperty]
    public bool HasPlayers => Players.Count > 0;

    [DataSourceProperty]
    public bool HasOtherFamilies => OtherFamilies.Count > 0;

    [DataSourceProperty]
    public string PlayersText => GetGroupText("str_coop_clan_players", Players.Count);

    [DataSourceProperty]
    public string OtherFamiliesText => GetGroupText("str_coop_clan_other_families", OtherFamilies.Count);

    public SharedClanMembersVM(Action onRefresh, Action<Hero> showHeroOnMap, IClanMemberGrouping grouping)
        : base(onRefresh, showHeroOnMap)
    {
        this.grouping = grouping;
        SortController._listsToControl.Insert(1, Players);
        SortController._listsToControl.Insert(2, OtherFamilies);
        RegroupMembers();
    }

    public void RegroupMembers()
    {
        // Vanilla also refreshes from its constructor, before the grouping service is assigned.
        if (grouping == null) return;

        Players.Clear();
        OtherFamilies.Clear();
        foreach (var member in Family.ToArray())
        {
            var group = grouping.GetGroup(member.GetHero(), Hero.MainHero);
            if (group == ClanMemberGroup.Family) continue;

            Family.Remove(member);
            if (group == ClanMemberGroup.Players)
                Players.Add(member);
            else
                OtherFamilies.Add(member);
        }

        RefreshGroupProperties();
    }

    public bool SelectAdditionalMember(Hero hero)
    {
        var member = Players.Concat(OtherFamilies).FirstOrDefault(item => item.GetHero() == hero);
        if (member == null) return false;

        OnMemberSelection(member);
        return true;
    }

    public override void RefreshValues()
    {
        base.RefreshValues();
        if (grouping == null) return;
        Players.ApplyActionOnAllItems(member => member.RefreshValues());
        OtherFamilies.ApplyActionOnAllItems(member => member.RefreshValues());
        RefreshGroupProperties();
    }

    public override void OnFinalize()
    {
        base.OnFinalize();
        Players.ApplyActionOnAllItems(member => member.OnFinalize());
        OtherFamilies.ApplyActionOnAllItems(member => member.OnFinalize());
    }

    private void RefreshGroupProperties()
    {
        FamilyText = GetGroupText("str_family_group", Family.Count);
        OnPropertyChanged(nameof(PlayersText));
        OnPropertyChanged(nameof(OtherFamiliesText));
        OnPropertyChanged(nameof(HasPlayers));
        OnPropertyChanged(nameof(HasOtherFamilies));
    }

    private string GetGroupText(string groupTextId, int count)
    {
        return GameTexts.FindText("str_RANK_with_NUM_between_parenthesis")
            .SetTextVariable("RANK", GameTexts.FindText(groupTextId))
            .SetTextVariable("NUMBER", count).ToString();
    }
}
