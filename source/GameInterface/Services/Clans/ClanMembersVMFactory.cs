using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;

namespace GameInterface.Services.Clans;

public interface IClanMembersVMFactory
{
    ClanMembersVM Create(Action onRefresh, Action<Hero> showHeroOnMap);
}

public class ClanMembersVMFactory : IClanMembersVMFactory
{
    private readonly IClanMemberGrouping grouping;

    public ClanMembersVMFactory(IClanMemberGrouping grouping)
    {
        this.grouping = grouping;
    }

    public ClanMembersVM Create(Action onRefresh, Action<Hero> showHeroOnMap)
    {
        return new SharedClanMembersVM(onRefresh, showHeroOnMap, grouping);
    }
}
