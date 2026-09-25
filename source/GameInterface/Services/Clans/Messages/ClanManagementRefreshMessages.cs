using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages;

[Flags]
public enum ClanManagementRefresh
{
    Members = 1,
    Parties = 2,
    Income = 4,
    Identity = 8,
    Finances = 16,
    All = Members | Parties | Income | Identity | Finances,
}

public readonly struct ClanManagementChanged : IEvent
{
    public readonly Clan Clan;
    public readonly ClanManagementRefresh Sections;

    public ClanManagementChanged(Clan clan, ClanManagementRefresh sections)
    {
        Clan = clan;
        Sections = sections;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRefreshClanManagement : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;
    [ProtoMember(2)]
    public readonly ClanManagementRefresh Sections;

    public NetworkRefreshClanManagement(string clanId, ClanManagementRefresh sections)
    {
        ClanId = clanId;
        Sections = sections;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRefreshAfterRoleAssignment : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    public NetworkRefreshAfterRoleAssignment(string mobilePartyId)
    {
        MobilePartyId = mobilePartyId;
    }
}
