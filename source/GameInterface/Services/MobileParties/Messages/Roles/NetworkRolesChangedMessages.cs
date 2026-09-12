using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MobileParties.Messages.Roles;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRemoveAllPartyRolesOfHero : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string MobilePartyId;

    public NetworkRemoveAllPartyRolesOfHero(string heroId, string mobilePartyId)
    {
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRemovePartyRoleOfHero : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string MobilePartyId;

    [ProtoMember(3)]
    public readonly PartyRole PartyRole;

    public NetworkRemovePartyRoleOfHero(string heroId, string mobilePartyId, PartyRole partyRole)
    {
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
        PartyRole = partyRole;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRemoveOnePartyRoleOfHero : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string MobilePartyId;

    public NetworkRemoveOnePartyRoleOfHero(string heroId, string mobilePartyId)
    {
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSetPartyScout : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string MobilePartyId;

    public NetworkSetPartyScout(string heroId, string mobilePartyId)
    {
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSetPartyQuartermaster : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string MobilePartyId;

    public NetworkSetPartyQuartermaster(string heroId, string mobilePartyId)
    {
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSetPartyEngineer : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string MobilePartyId;

    public NetworkSetPartyEngineer(string heroId, string mobilePartyId)
    {
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSetPartySurgeon : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string MobilePartyId;

    public NetworkSetPartySurgeon(string heroId, string mobilePartyId)
    {
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSetPartyFirstMate : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string MobilePartyId;

    public NetworkSetPartyFirstMate(string heroId, string mobilePartyId)
    {
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSetPartyNavigator : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string MobilePartyId;

    public NetworkSetPartyNavigator(string heroId, string mobilePartyId)
    {
        HeroId = heroId;
        MobilePartyId = mobilePartyId;
    }
}