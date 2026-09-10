using Common.Messaging;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;

namespace GameInterface.Services.Clans;

public interface IClanMembersVMFactory : IGameAbstraction
{
    ClanMembersVM Create(Action onRefresh, Action<Hero> showHeroOnMap);
}

public class ClanMembersVMFactory : IClanMembersVMFactory
{
    private readonly IClanMemberGrouping grouping;
    private readonly IClanLeaveRules leaveRules;
    private readonly IMessageBroker messageBroker;

    public ClanMembersVMFactory(IClanMemberGrouping grouping, IClanLeaveRules leaveRules, IMessageBroker messageBroker)
    {
        this.grouping = grouping;
        this.leaveRules = leaveRules;
        this.messageBroker = messageBroker;
    }

    public ClanMembersVM Create(Action onRefresh, Action<Hero> showHeroOnMap)
    {
        return new CoopClanMembersVM(onRefresh, showHeroOnMap, grouping, leaveRules, messageBroker);
    }
}
