using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRefreshPartiesList : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    public NetworkRefreshPartiesList(string clanId)
    {
        ClanId = clanId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRefreshWorkshopsList : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    public NetworkRefreshWorkshopsList(string clanId)
    {
        ClanId = clanId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRefreshClanMembersList : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    public NetworkRefreshClanMembersList(string clanId)
    {
        ClanId = clanId;
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
