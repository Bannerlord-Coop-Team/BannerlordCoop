using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Heroes.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Clans;

public class CoopClanMembersVM : ClanMembersVM
{
    private readonly IClanMemberGrouping grouping;
    private readonly IClanLeaveRules leaveRules;
    private readonly IMessageBroker messageBroker;

    [DataSourceProperty]
    public bool CanLeaveClan => leaveRules?.CanLeave(Hero.MainHero) == true;

    [DataSourceProperty]
    public bool CanManagePlayer => Hero.MainHero != null && Hero.MainHero.Clan?.Leader == Hero.MainHero &&
        CurrentSelectedMember?.GetHero() is Hero hero && hero != Hero.MainHero &&
        hero.Clan == Hero.MainHero.Clan && hero.IsPlayerHero();

    [DataSourceProperty]
    public string LeaveClanText => GameTexts.FindText("str_coop_clan_leave").ToString();

    [DataSourceProperty]
    public string ManagePlayerText => GameTexts.FindText("str_coop_clan_manage_player").ToString();

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

    public CoopClanMembersVM(Action onRefresh, Action<Hero> showHeroOnMap, IClanMemberGrouping grouping,
        IClanLeaveRules leaveRules, IMessageBroker messageBroker)
        : base(onRefresh, showHeroOnMap)
    {
        this.grouping = grouping;
        this.leaveRules = leaveRules;
        this.messageBroker = messageBroker;
        SortController._listsToControl.Insert(0, Players);
        SortController._listsToControl.Insert(2, OtherFamilies);
        RegroupMembers();
    }

    public void ExecuteLeaveClan()
    {
        if (CanLeaveClan)
            messageBroker.Publish(this, new ClanMemberLeaveRequested(Hero.MainHero, Hero.MainHero));
    }

    public void ExecuteManagePlayer()
    {
        if (!CanManagePlayer) return;
        var actor = Hero.MainHero;
        var member = CurrentSelectedMember.GetHero();
        bool canRemove = leaveRules.CanRemove(actor, member);
        var options = new List<InquiryElement>
        {
            new InquiryElement(member, GameTexts.FindText("str_coop_clan_remove_player").ToString(), null,
                canRemove, canRemove ? string.Empty : GameTexts.FindText("str_coop_marriage_clan_commitment").ToString())
        };
        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            GameTexts.FindText("str_coop_clan_manage_player_title").SetTextVariable("HERO", member.Name).ToString(),
            GameTexts.FindText("str_coop_clan_experimental_warning") + "\n\n" +
                GameTexts.FindText("str_coop_clan_remove_player_description"),
            options, true, 1, 1, GameTexts.FindText("str_coop_clan_remove_player").ToString(),
            GameTexts.FindText("str_cancel").ToString(),
            _ =>
            {
                if (leaveRules.CanRemove(actor, member))
                    messageBroker.Publish(this, new ClanMemberLeaveRequested(actor, member));
            }, null));
    }

    public void RefreshPlayerActions()
    {
        OnPropertyChanged(nameof(CanLeaveClan));
        OnPropertyChanged(nameof(CanManagePlayer));
        OnPropertyChanged(nameof(LeaveClanText));
        OnPropertyChanged(nameof(ManagePlayerText));
    }

    public void RegroupMembers()
    {
        // Vanilla also refreshes from its constructor, before the grouping service is assigned.
        if (grouping == null) return;

        Players.Clear();
        OtherFamilies.Clear();
        var viewer = Hero.MainHero;
        if (Family.Count(member => grouping.GetGroup(member.GetHero(), viewer) == ClanMemberGroup.Players) <= 1)
        {
            RefreshGroupProperties();
            return;
        }

        foreach (var member in Family.ToArray())
        {
            var group = grouping.GetGroup(member.GetHero(), viewer);
            if (group == ClanMemberGroup.Family) continue;

            Family.Remove(member);
            if (group == ClanMemberGroup.Players)
            {
                if (member.GetHero() == viewer)
                    Players.Insert(0, member);
                else
                    Players.Add(member);
            }
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
        RefreshPlayerActions();
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
