using Common.Messaging;
using ProtoBuf;
using GameInterface.Services.Clans.Data;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct UpdatePartyConfigurationOnSelection : ICommand
{
    [ProtoMember(1)]
    public readonly string LeaderHeroId;

    [ProtoMember(2)]
    public readonly PartyConfigurationFlag Flag;

    [ProtoMember(3)]
    public readonly bool Value;

    public UpdatePartyConfigurationOnSelection(string leaderHeroId, PartyConfigurationFlag flag, bool value)
    {
        LeaderHeroId = leaderHeroId;
        Flag = flag;
        Value = value;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct ChangeAutoRecruitForSettlement : ICommand
{
    [ProtoMember(1)]
    public readonly string HomeSettlementId;

    [ProtoMember(2)]
    public readonly bool Value;

    public ChangeAutoRecruitForSettlement(
        string homeSettlementId,
        bool value)
    {
        HomeSettlementId = homeSettlementId;
        Value = value;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct ChangeAutoRecruitForSettlementClients : ICommand
{
    [ProtoMember(1)]
    public readonly string HomeSettlementId;

    [ProtoMember(2)]
    public readonly bool Value;

    public ChangeAutoRecruitForSettlementClients(
        string homeSettlementId,
        bool value)
    {
        HomeSettlementId = homeSettlementId;
        Value = value;
    }
}