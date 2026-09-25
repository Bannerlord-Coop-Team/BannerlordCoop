using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages;

public readonly struct ClanFinanceChangeRequested : IEvent
{
    public readonly Hero Actor;
    public readonly Hero Member;
    public readonly Clan Clan;
    public readonly int Amount;
    public readonly bool IsTribute;

    public ClanFinanceChangeRequested(
        Hero actor,
        Hero member,
        Clan clan,
        int amount,
        bool isTribute)
    {
        Actor = actor;
        Member = member;
        Clan = clan;
        Amount = amount;
        IsTribute = isTribute;
    }
}

public readonly struct ClanFinanceChanged : IEvent
{
    public readonly string MemberId;
    public readonly ClanFinanceSettings Settings;

    public ClanFinanceChanged(
        string memberId,
        ClanFinanceSettings settings)
    {
        MemberId = memberId;
        Settings = settings;
    }
}

public readonly struct InitializeClientClanFinance : IEvent
{
    public readonly Dictionary<string, ClanFinanceSettings> Settings;

    public InitializeClientClanFinance(Dictionary<string, ClanFinanceSettings> settings)
    {
        Settings = settings;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RequestClanFinanceChange : ICommand
{
    [ProtoMember(1)]
    public readonly string ActorId;

    [ProtoMember(2)]
    public readonly string MemberId;

    [ProtoMember(3)]
    public readonly string ClanId;

    [ProtoMember(4)]
    public readonly int Amount;

    [ProtoMember(5)]
    public readonly bool IsTribute;

    public RequestClanFinanceChange(
        string actorId,
        string memberId,
        string clanId,
        int amount,
        bool isTribute)
    {
        ActorId = actorId;
        MemberId = memberId;
        ClanId = clanId;
        Amount = amount;
        IsTribute = isTribute;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkClanFinanceChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string MemberId;

    [ProtoMember(2)]
    public readonly ClanFinanceSettings Settings;

    public NetworkClanFinanceChanged(
        string memberId,
        ClanFinanceSettings settings)
    {
        MemberId = memberId;
        Settings = settings;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkClanFinanceConnectionsChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string[] DisconnectedHeroIds;

    public NetworkClanFinanceConnectionsChanged(string[] disconnectedHeroIds)
    {
        DisconnectedHeroIds = disconnectedHeroIds;
    }
}
