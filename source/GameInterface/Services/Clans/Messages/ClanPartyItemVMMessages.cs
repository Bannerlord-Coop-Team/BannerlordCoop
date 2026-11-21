using Common.Messaging;
using GameInterface.Services.Clans.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Clans.Messages;

public readonly struct PartyConfigurationChangedOnSelection : IEvent
{
    public readonly Hero Leader;
    public readonly PartyConfigurationFlag Flag;
    public readonly bool Value;

    public PartyConfigurationChangedOnSelection(Hero leader, PartyConfigurationFlag flag, bool value)
    {
        Leader = leader;
        Flag = flag;
        Value = value;
    }
}

public readonly struct AutoRecruitChangedForSettlement : IEvent
{
    public readonly Settlement HomeSettlement;
    public readonly bool Value;

    public AutoRecruitChangedForSettlement(Settlement homeSettlement, bool value)
    {
        HomeSettlement = homeSettlement;
        Value = value;
    }
}